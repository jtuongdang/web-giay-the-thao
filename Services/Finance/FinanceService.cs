using Microsoft.EntityFrameworkCore;
using QuanLyBanGiay.Data;
using QuanLyBanGiay.Models.DTOs;
using QuanLyBanGiay.Models.Entities;
using QuanLyBanGiay.Services.Inventory;

namespace QuanLyBanGiay.Services.Finance;

/// <summary>Manages supplier balances, supplier payments, and cash-flow reporting.</summary>
public sealed class FinanceService(AppDbContext db)
{
    public async Task<IReadOnlyList<SupplierDto>> GetSuppliersAsync(CancellationToken cancellationToken = default) =>
        await db.Suppliers.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new SupplierDto
            {
                Id = x.Id, Name = x.Name, PhoneNumber = x.Phone, Email = x.Email,
                Address = x.Address, CurrentDebt = x.CurrentDebt, IsActive = true
            }).ToListAsync(cancellationToken);

    public async Task<SupplierDto> CreateSupplierAsync(CreateSupplierRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Supplier name is required.", nameof(request));

        var supplier = new Supplier
        {
            Code = InventoryService.NewCode("SUP"), Name = request.Name.Trim(),
            Phone = request.PhoneNumber?.Trim(), Email = request.Email?.Trim(),
            Address = request.Address?.Trim(), CurrentDebt = 0
        };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(cancellationToken);
        return new SupplierDto
        {
            Id = supplier.Id, Name = supplier.Name, PhoneNumber = supplier.Phone,
            Email = supplier.Email, Address = supplier.Address, CurrentDebt = supplier.CurrentDebt, IsActive = true
        };
    }

    public async Task<SupplierPaymentDto> PaySupplierAsync(
        int supplierId,
        CreateSupplierPaymentRequest request,
        int? createdByUserId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Amount <= 0 || decimal.Round(request.Amount, 2) != request.Amount)
            throw new ArgumentOutOfRangeException(nameof(request), "Payment amount must be positive and have at most two decimal places.");
        if (string.IsNullOrWhiteSpace(request.PaymentMethod))
            throw new ArgumentException("Payment method is required.", nameof(request));

        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var supplier = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == supplierId, cancellationToken)
            ?? throw new KeyNotFoundException($"Supplier {supplierId} was not found.");
        if (supplier.CurrentDebt < request.Amount)
            throw new InvalidOperationException("Payment amount cannot exceed the supplier's outstanding debt.");

        PurchaseOrder? explicitlySelectedOrder = null;
        if (request.PurchaseOrderId.HasValue)
        {
            explicitlySelectedOrder = await db.PurchaseOrders
                .SingleOrDefaultAsync(x => x.Id == request.PurchaseOrderId.Value, cancellationToken)
                ?? throw new KeyNotFoundException($"Purchase order {request.PurchaseOrderId.Value} was not found.");
            if (explicitlySelectedOrder.SupplierId != supplierId || explicitlySelectedOrder.Status != "Received")
                throw new InvalidOperationException("The selected purchase order must be received and belong to this supplier.");
            if (explicitlySelectedOrder.TotalAmount - explicitlySelectedOrder.PaidAmount < request.Amount)
                throw new InvalidOperationException("Payment amount cannot exceed the selected purchase order's outstanding amount.");
        }

        var remaining = request.Amount;
        var payment = new SupplierPayment
        {
            PaymentCode = InventoryService.NewCode("SP"), SupplierId = supplierId,
            PurchaseOrderId = explicitlySelectedOrder?.Id, Amount = request.Amount,
            PaymentMethod = request.PaymentMethod.Trim(), PaidAt = DateTime.UtcNow,
            Note = JoinNote(request.Note, request.Reference)
        };
        db.SupplierPayments.Add(payment);

        if (explicitlySelectedOrder is not null)
        {
            explicitlySelectedOrder.PaidAmount += remaining;
            explicitlySelectedOrder.PaymentStatus = InventoryService.PaymentStatus(explicitlySelectedOrder.PaidAmount, explicitlySelectedOrder.TotalAmount);
        }
        else
        {
            var orders = await db.PurchaseOrders
                .Where(x => x.SupplierId == supplierId && x.Status == "Received" && x.PaidAmount < x.TotalAmount)
                .OrderBy(x => x.OrderDate).ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);
            foreach (var order in orders)
            {
                var balance = order.TotalAmount - order.PaidAmount;
                var applied = Math.Min(balance, remaining);
                if (applied <= 0) continue;
                order.PaidAmount += applied;
                order.PaymentStatus = InventoryService.PaymentStatus(order.PaidAmount, order.TotalAmount);
                remaining -= applied;
                if (remaining == 0) break;
            }
            if (remaining != 0)
                throw new InvalidOperationException("Supplier debt does not match the outstanding received purchase orders.");
        }

        supplier.CurrentDebt -= request.Amount;
        db.CashFlowLedger.Add(new CashFlowLedger
        {
            VoucherCode = InventoryService.NewCode("CF"), VoucherType = "PAYMENT",
            Category = "SUPPLIER_PAYMENT", Amount = request.Amount,
            PaymentMethod = request.PaymentMethod.Trim(), ReferenceType = "SupplierPayment",
            ReferenceId = 0, SupplierPayment = payment, PurchaseOrderId = explicitlySelectedOrder?.Id,
            CreatedByUserId = createdByUserId, CreatedAt = DateTime.UtcNow,
            Note = string.IsNullOrWhiteSpace(request.Note) ? $"Payment to {supplier.Name}" : request.Note.Trim()
        });
        await db.SaveChangesAsync(cancellationToken);
        // ReferenceId is the generated supplier payment ID; save once more in the same transaction.
        var ledger = await db.CashFlowLedger.SingleAsync(x => x.SupplierPaymentId == payment.Id, cancellationToken);
        ledger.ReferenceId = payment.Id;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new SupplierPaymentDto
        {
            Id = payment.Id, SupplierId = payment.SupplierId, PurchaseOrderId = payment.PurchaseOrderId,
            Amount = payment.Amount, PaymentMethod = payment.PaymentMethod,
            Reference = request.Reference, PaidAt = new DateTimeOffset(DateTime.SpecifyKind(payment.PaidAt, DateTimeKind.Utc))
        };
    }

    public async Task<FinanceSummaryDto> GetSummaryAsync(
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken cancellationToken = default)
    {
        if (periodEnd < periodStart) throw new ArgumentException("Period end must not precede period start.");
        var start = periodStart.UtcDateTime;
        var end = periodEnd.UtcDateTime;
        var orders = await db.Orders.AsNoTracking()
            .Where(x => x.CreatedAt >= start && x.CreatedAt <= end && x.OrderStatus == "Completed")
            .Select(x => new { Revenue = x.Subtotal - x.DiscountAmount, Cogs = x.Items.Sum(i => i.UnitCost * i.Quantity) })
            .ToListAsync(cancellationToken);
        var ledger = await db.CashFlowLedger.AsNoTracking()
            .Where(x => x.CreatedAt >= start && x.CreatedAt <= end)
            .GroupBy(x => x.VoucherType)
            .Select(g => new { Type = g.Key, Total = g.Sum(x => x.Amount) })
            .ToListAsync(cancellationToken);
        var inflow = ledger.Where(x => x.Type == "RECEIPT").Sum(x => x.Total);
        var outflow = ledger.Where(x => x.Type == "PAYMENT").Sum(x => x.Total);
        var revenue = orders.Sum(x => x.Revenue);
        var cogs = orders.Sum(x => x.Cogs);
        return new FinanceSummaryDto
        {
            NetRevenue = revenue, TotalCogs = cogs, GrossProfit = revenue - cogs,
            CashInflow = inflow, CashOutflow = outflow, NetCashFlow = inflow - outflow,
            SupplierDebt = await db.Suppliers.SumAsync(x => (decimal?)x.CurrentDebt, cancellationToken) ?? 0,
            PeriodStart = periodStart, PeriodEnd = periodEnd
        };
    }

    public async Task<IReadOnlyList<CashLedgerEntryDto>> GetCashLedgerAsync(
        DateTimeOffset? periodStart = null,
        DateTimeOffset? periodEnd = null,
        CancellationToken cancellationToken = default)
    {
        var query = db.CashFlowLedger.AsNoTracking().AsQueryable();
        if (periodStart.HasValue) query = query.Where(x => x.CreatedAt >= periodStart.Value.UtcDateTime);
        if (periodEnd.HasValue) query = query.Where(x => x.CreatedAt <= periodEnd.Value.UtcDateTime);
        return await query.OrderByDescending(x => x.CreatedAt).Select(x => new CashLedgerEntryDto
        {
            Id = x.Id, Direction = x.VoucherType == "RECEIPT" ? "INFLOW" : "OUTFLOW",
            Category = x.Category, Amount = x.Amount, ReferenceType = x.ReferenceType,
            ReferenceId = x.ReferenceId, Description = x.Note ?? string.Empty,
            CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(x.CreatedAt, DateTimeKind.Utc)),
            CreatedBy = x.CreatedByUserId == null ? null : db.Users.Where(u => u.Id == x.CreatedByUserId).Select(u => u.FullName).FirstOrDefault()
        }).ToListAsync(cancellationToken);
    }

    public async Task<CashLedgerEntryDto> CreateCashLedgerEntryAsync(
        CreateCashLedgerRequest request,
        int? createdByUserId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var direction = request.Direction.Trim().ToUpperInvariant();
        if (direction is not ("INFLOW" or "OUTFLOW") || request.Amount <= 0 ||
            string.IsNullOrWhiteSpace(request.Category) || string.IsNullOrWhiteSpace(request.Description))
            throw new ArgumentException("A valid direction, positive amount, category, and description are required.", nameof(request));

        var entry = new CashFlowLedger
        {
            VoucherCode = InventoryService.NewCode("CF"), VoucherType = direction == "INFLOW" ? "RECEIPT" : "PAYMENT",
            Category = request.Category.Trim(), Amount = decimal.Round(request.Amount, 2), PaymentMethod = "Cash",
            ReferenceType = "Manual", ReferenceId = 0, CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow, Note = request.Description.Trim()
        };
        db.CashFlowLedger.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return new CashLedgerEntryDto
        {
            Id = entry.Id, Direction = direction, Category = entry.Category, Amount = entry.Amount,
            ReferenceType = entry.ReferenceType, ReferenceId = entry.ReferenceId,
            Description = entry.Note!, CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(entry.CreatedAt, DateTimeKind.Utc))
        };
    }

    private static string? JoinNote(string? note, string? reference)
    {
        var values = new[] { note?.Trim(), string.IsNullOrWhiteSpace(reference) ? null : $"Reference: {reference.Trim()}" }
            .Where(x => !string.IsNullOrWhiteSpace(x));
        var result = string.Join(" | ", values);
        return result.Length == 0 ? null : result;
    }
}
