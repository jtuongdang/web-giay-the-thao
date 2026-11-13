using Microsoft.EntityFrameworkCore;
using QuanLyBanGiay.Data;
using QuanLyBanGiay.Models.DTOs;
using QuanLyBanGiay.Models.Entities;

namespace QuanLyBanGiay.Services.Inventory;

/// <summary>Creates and receives purchase orders and records stock movements.</summary>
public sealed class InventoryService(AppDbContext db)
{
    public async Task<PurchaseOrderDto> CreatePurchaseOrderAsync(
        CreatePurchaseOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SupplierId <= 0 || request.Items.Count == 0)
            throw new ArgumentException("A supplier and at least one purchase order item are required.", nameof(request));

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (!await db.Suppliers.AnyAsync(x => x.Id == request.SupplierId, cancellationToken))
            throw new KeyNotFoundException($"Supplier {request.SupplierId} was not found.");

        var variantIds = request.Items.Select(x => x.VariantId).Distinct().ToArray();
        var existingVariants = await db.ProductVariants
            .Where(x => variantIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        if (existingVariants.Count != variantIds.Length)
            throw new KeyNotFoundException("One or more product variants were not found.");

        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0 || item.UnitCost < 0 || decimal.Round(item.UnitCost, 2) != item.UnitCost)
                throw new ArgumentException("Item quantities must be positive and unit costs must be non-negative with at most two decimal places.", nameof(request));
        }

        var items = request.Items.Select(x => new PurchaseOrderItem
        {
            ProductVariantId = x.VariantId,
            Quantity = x.Quantity,
            UnitCost = x.UnitCost,
            TotalCost = decimal.Round(x.Quantity * x.UnitCost, 2, MidpointRounding.AwayFromZero)
        }).ToList();
        var order = new PurchaseOrder
        {
            PoCode = NewCode("PO"),
            SupplierId = request.SupplierId,
            TotalAmount = items.Sum(x => x.TotalCost),
            PaidAmount = 0,
            PaymentStatus = "Unpaid",
            Status = "Draft",
            OrderDate = DateTime.UtcNow,
            Items = items
        };
        db.PurchaseOrders.Add(order);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ToDtoAsync(order.Id, cancellationToken);
    }

    public async Task<PurchaseOrderDto> ReceivePurchaseOrderAsync(
        int purchaseOrderId,
        ReceivePurchaseOrderRequest? request = null,
        int? createdByUserId = null,
        CancellationToken cancellationToken = default)
    {
        request ??= new ReceivePurchaseOrderRequest();
        if (request.PaidAmount < 0 || decimal.Round(request.PaidAmount, 2) != request.PaidAmount)
            throw new ArgumentOutOfRangeException(nameof(request), "Paid amount must be non-negative and have at most two decimal places.");

        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var order = await db.PurchaseOrders
            .Include(x => x.Supplier)
            .Include(x => x.Items)
            .ThenInclude(x => x.ProductVariant)
            .SingleOrDefaultAsync(x => x.Id == purchaseOrderId, cancellationToken)
            ?? throw new KeyNotFoundException($"Purchase order {purchaseOrderId} was not found.");

        if (!string.Equals(order.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only a draft purchase order can be received.");
        if (request.PaidAmount > order.TotalAmount)
            throw new ArgumentOutOfRangeException(nameof(request), "Paid amount cannot exceed the purchase order total.");

        foreach (var group in order.Items.GroupBy(x => x.ProductVariantId))
        {
            var lines = group.ToList();
            var variant = lines[0].ProductVariant;
            var stockBefore = variant.StockQuantity;
            var receivedQuantity = lines.Sum(x => x.Quantity);
            var stockAfter = checked(stockBefore + receivedQuantity);
            var receivedValue = lines.Sum(x => x.Quantity * x.UnitCost);
            var previousInventoryValue = stockBefore * variant.CostPrice;
            variant.CostPrice = stockAfter == 0
                ? 0
                : decimal.Round((previousInventoryValue + receivedValue) / stockAfter, 2, MidpointRounding.AwayFromZero);
            variant.StockQuantity = stockAfter;
            variant.UpdatedAt = DateTime.UtcNow;

            var quantityAfterLine = stockBefore;
            foreach (var item in lines)
            {
                var beforeLine = quantityAfterLine;
                quantityAfterLine = checked(quantityAfterLine + item.Quantity);
                db.InventoryTransactions.Add(new InventoryTransaction
                {
                    TxCode = NewCode("INV"),
                    ProductVariantId = variant.Id,
                    TxType = "IMPORT",
                    QuantityChange = item.Quantity,
                    QuantityBefore = beforeLine,
                    QuantityAfter = quantityAfterLine,
                    ReferenceType = "PurchaseOrder",
                    ReferenceId = order.Id,
                    CreatedByUserId = createdByUserId,
                    CreatedAt = DateTime.UtcNow,
                    Note = $"Received {order.PoCode}"
                });
            }
        }

        order.PaidAmount = request.PaidAmount;
        order.PaymentStatus = PaymentStatus(order.PaidAmount, order.TotalAmount);
        order.Status = "Received";
        order.ReceivedDate = DateTime.UtcNow;
        order.Supplier.CurrentDebt += order.TotalAmount - order.PaidAmount;
        if (order.PaidAmount > 0)
        {
            db.CashFlowLedger.Add(new CashFlowLedger
            {
                VoucherCode = NewCode("CF"),
                VoucherType = "PAYMENT",
                Category = "SUPPLIER_PAYMENT",
                Amount = order.PaidAmount,
                PaymentMethod = "Cash",
                ReferenceType = "PurchaseOrder",
                ReferenceId = order.Id,
                PurchaseOrderId = order.Id,
                CreatedByUserId = createdByUserId,
                CreatedAt = DateTime.UtcNow,
                Note = $"Initial payment for {order.PoCode}"
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ToDtoAsync(order.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryItemDto>> GetInventoryAsync(CancellationToken cancellationToken = default) =>
        await db.ProductVariants.AsNoTracking()
            .OrderBy(x => x.Product.Name).ThenBy(x => x.Size).ThenBy(x => x.Color)
            .Select(x => new InventoryItemDto
            {
                VariantId = x.Id,
                ProductId = x.ProductId,
                ProductName = x.Product.Name,
                Sku = x.Product.Sku,
                Size = x.Size,
                Color = x.Color,
                StockQuantity = x.StockQuantity,
                ReservedQuantity = x.ReservedQuantity,
                AvailableQuantity = x.StockQuantity - x.ReservedQuantity,
                CostPrice = x.CostPrice,
                IsLowStock = x.StockQuantity - x.ReservedQuantity <= x.MinStockAlert
            }).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<InventoryTransactionDto>> GetTransactionsAsync(CancellationToken cancellationToken = default) =>
        await db.InventoryTransactions.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new InventoryTransactionDto
            {
                Id = x.Id,
                VariantId = x.ProductVariantId,
                ProductName = x.ProductVariant.Product.Name,
                TransactionType = x.TxType,
                QuantityChange = x.QuantityChange,
                StockAfter = x.QuantityAfter,
                Note = x.Note,
                CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(x.CreatedAt, DateTimeKind.Utc)),
                CreatedBy = x.CreatedByUserId == null ? null : db.Users.Where(u => u.Id == x.CreatedByUserId).Select(u => u.FullName).FirstOrDefault()
            }).ToListAsync(cancellationToken);

    public async Task<InventoryItemDto> AdjustInventoryAsync(
        AdjustInventoryRequest request,
        int? createdByUserId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.QuantityChange == 0 || string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("A non-zero quantity change and reason are required.", nameof(request));

        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var variant = await db.ProductVariants.Include(x => x.Product)
            .SingleOrDefaultAsync(x => x.Id == request.VariantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Product variant {request.VariantId} was not found.");
        var before = variant.StockQuantity;
        var after = checked(before + request.QuantityChange);
        if (after < variant.ReservedQuantity)
            throw new InvalidOperationException("Adjustment cannot reduce stock below the reserved quantity.");

        variant.StockQuantity = after;
        variant.UpdatedAt = DateTime.UtcNow;
        db.InventoryTransactions.Add(new InventoryTransaction
        {
            TxCode = NewCode("INV"), ProductVariantId = variant.Id, TxType = "ADJUSTMENT",
            QuantityChange = request.QuantityChange, QuantityBefore = before, QuantityAfter = after,
            ReferenceType = "Manual", ReferenceId = variant.Id, CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow, Note = request.Reason
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new InventoryItemDto
        {
            VariantId = variant.Id, ProductId = variant.ProductId, ProductName = variant.Product.Name,
            Sku = variant.Product.Sku, Size = variant.Size, Color = variant.Color,
            StockQuantity = after, ReservedQuantity = variant.ReservedQuantity,
            AvailableQuantity = after - variant.ReservedQuantity, CostPrice = variant.CostPrice,
            IsLowStock = after - variant.ReservedQuantity <= variant.MinStockAlert
        };
    }

    private async Task<PurchaseOrderDto> ToDtoAsync(int id, CancellationToken cancellationToken)
    {
        var order = await db.PurchaseOrders.AsNoTracking().Include(x => x.Supplier).Include(x => x.Items)
            .SingleAsync(x => x.Id == id, cancellationToken);
        return new PurchaseOrderDto
        {
            Id = order.Id, OrderCode = order.PoCode, SupplierId = order.SupplierId,
            SupplierName = order.Supplier.Name, Status = order.Status, TotalAmount = order.TotalAmount,
            PaidAmount = order.PaidAmount,
            CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(order.OrderDate, DateTimeKind.Utc)),
            Items = order.Items.Select(x => new CreatePurchaseOrderItemRequest
            {
                VariantId = x.ProductVariantId, Quantity = x.Quantity, UnitCost = x.UnitCost
            }).ToArray()
        };
    }

    internal static string PaymentStatus(decimal paid, decimal total) => paid <= 0 ? "Unpaid" : paid >= total ? "Paid" : "Partial";
    internal static string NewCode(string prefix) => $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
}
