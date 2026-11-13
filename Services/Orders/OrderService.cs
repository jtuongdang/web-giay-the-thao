using Microsoft.EntityFrameworkCore;
using QuanLyBanGiay.Data;
using QuanLyBanGiay.Models.DTOs;
using QuanLyBanGiay.Models.Entities;
using QuanLyBanGiay.Services.Payment;

namespace QuanLyBanGiay.Services.Orders;

public sealed class OrderService(AppDbContext db)
{
    private static readonly HashSet<string> PaymentMethods =
        new(StringComparer.OrdinalIgnoreCase) { "COD", "QR_TRANSFER", "MOCK_CARD" };

    public async Task<OrderCreatedResponse> CreateOrderAsync(
        CreateOrderRequest request,
        int? userId = null,
        decimal shippingFee = 0m,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCustomer(request);

        if (request.Items.Count == 0 || request.Items.Any(x => x.VariantId <= 0 || x.Quantity <= 0))
            throw new InvalidOperationException("Đơn hàng phải có ít nhất một sản phẩm với số lượng hợp lệ.");
        if (shippingFee < 0)
            throw new ArgumentOutOfRangeException(nameof(shippingFee));
        if (!PaymentMethods.Contains(request.PaymentMethod))
            throw new InvalidOperationException("Phương thức thanh toán không được hỗ trợ.");

        var requestedItems = request.Items
            .GroupBy(x => x.VariantId)
            .Select(g => new { VariantId = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToArray();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var variantIds = requestedItems.Select(x => x.VariantId).ToArray();
            var variants = await db.ProductVariants
                .Include(x => x.Product)
                .Where(x => variantIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, cancellationToken);

            if (variants.Count != requestedItems.Length)
                throw new InvalidOperationException("Một hoặc nhiều biến thể sản phẩm không tồn tại.");

            foreach (var requested in requestedItems)
            {
                var variant = variants[requested.VariantId];
                if (!variant.Product.IsActive ||
                    variant.StockQuantity - variant.ReservedQuantity < requested.Quantity)
                    throw new InvalidOperationException($"Biến thể {variant.VariantSku} không đủ tồn kho khả dụng.");

                variant.ReservedQuantity += requested.Quantity;
                variant.UpdatedAt = DateTime.UtcNow;
            }

            var now = DateTime.UtcNow;
            var subtotal = requestedItems.Sum(x => variants[x.VariantId].Product.RetailPrice * x.Quantity);
            var discountAmount = 0m;
            Voucher? voucher = null;
            if (!string.IsNullOrWhiteSpace(request.VoucherCode))
            {
                voucher = await db.Vouchers
                    .AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Code == request.VoucherCode.Trim(), cancellationToken);
                if (voucher is null || !voucher.IsActive || now < voucher.StartAt || now > voucher.EndAt ||
                    voucher.UsedCount >= voucher.UsageLimit || subtotal < voucher.MinOrderAmount)
                    throw new InvalidOperationException("Mã giảm giá không hợp lệ hoặc không còn lượt sử dụng.");

                discountAmount = voucher.DiscountType switch
                {
                    "PERCENT" => subtotal * voucher.DiscountValue / 100m,
                    "FIXED_AMOUNT" => voucher.DiscountValue,
                    _ => throw new InvalidOperationException("Loại mã giảm giá không được hỗ trợ.")
                };
                if (voucher.MaxDiscountAmount is decimal maxDiscount)
                    discountAmount = Math.Min(discountAmount, maxDiscount);
                discountAmount = Math.Clamp(decimal.Round(discountAmount, 2), 0m, subtotal);
                var reservedVoucherUses = await db.Vouchers
                    .Where(x => x.Id == voucher.Id && x.IsActive &&
                                x.StartAt <= now && x.EndAt >= now &&
                                x.UsedCount < x.UsageLimit)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(x => x.UsedCount, x => x.UsedCount + 1),
                        cancellationToken);
                if (reservedVoucherUses != 1)
                    throw new InvalidOperationException("Mã giảm giá vừa hết lượt sử dụng.");
            }

            var order = new Order
            {
                OrderCode = NewOrderCode(now),
                UserId = userId,
                VoucherId = voucher?.Id,
                VoucherCode = voucher?.Code,
                CustomerName = request.CustomerName.Trim(),
                CustomerPhone = request.CustomerPhone.Trim(),
                ShippingAddress = request.ShippingAddress.Trim(),
                Subtotal = subtotal,
                DiscountAmount = discountAmount,
                ShippingFee = shippingFee,
                TotalAmount = subtotal - discountAmount + shippingFee,
                PaymentMethod = request.PaymentMethod.Trim().ToUpperInvariant(),
                PaymentStatus = "Pending",
                OrderStatus = "Pending",
                CreatedAt = now,
                Items = requestedItems.Select(x =>
                {
                    var variant = variants[x.VariantId];
                    return new OrderItem
                    {
                        ProductVariantId = variant.Id,
                        ProductName = variant.Product.Name,
                        Size = variant.Size,
                        Color = variant.Color,
                        UnitPrice = variant.Product.RetailPrice,
                        UnitCost = variant.CostPrice,
                        Quantity = x.Quantity,
                        TotalPrice = variant.Product.RetailPrice * x.Quantity
                    };
                }).ToList()
            };

            db.Orders.Add(order);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new OrderCreatedResponse
            {
                OrderCode = order.OrderCode,
                TotalAmount = order.TotalAmount,
                QrCodeData = order.PaymentMethod == "QR_TRANSFER"
                    ? $"MOCKQR|{order.OrderCode}|{order.TotalAmount:0.00}"
                    : null
            };
        }
        catch (DbUpdateConcurrencyException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new InvalidOperationException("Tồn kho vừa được cập nhật bởi giao dịch khác; vui lòng thử lại.", exception);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<Order> UpdateOrderStatusAsync(
        string orderCode,
        string targetStatus,
        string? note = null,
        CancellationToken cancellationToken = default)
    {
        return await UpdateOrderStatusAsync(
            orderCode, targetStatus, note, expectedCurrentStatus: null, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Không thể cập nhật trạng thái đơn hàng.");
    }

    private async Task<Order?> UpdateOrderStatusAsync(
        string orderCode,
        string targetStatus,
        string? note,
        string? expectedCurrentStatus,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(orderCode))
            throw new ArgumentException("Mã đơn hàng là bắt buộc.", nameof(orderCode));
        if (string.IsNullOrWhiteSpace(targetStatus))
            throw new ArgumentException("Trạng thái mới là bắt buộc.", nameof(targetStatus));

        var status = targetStatus.Trim().ToLowerInvariant() switch
        {
            "pending" => "Pending",
            "confirmed" => "Confirmed",
            "shipping" => "Shipping",
            "completed" => "Completed",
            "cancelled" => "Cancelled",
            _ => throw new InvalidOperationException($"Trạng thái {targetStatus} không hợp lệ.")
        };
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var order = await db.Orders
                .Include(x => x.Items)
                .SingleOrDefaultAsync(x => x.OrderCode == orderCode, cancellationToken)
                ?? throw new KeyNotFoundException($"Không tìm thấy đơn hàng {orderCode}.");
            if (expectedCurrentStatus is not null && order.OrderStatus != expectedCurrentStatus)
                return null;
            var now = DateTime.UtcNow;

            if (order.OrderStatus == "Pending" && status == "Confirmed")
            {
                var variants = await LoadOrderVariantsAsync(order, cancellationToken);
                foreach (var group in order.Items.GroupBy(x => x.ProductVariantId))
                {
                    var variant = variants[group.Key];
                    var quantity = group.Sum(x => x.Quantity);
                    if (variant.ReservedQuantity < quantity || variant.StockQuantity < quantity)
                        throw new InvalidOperationException("Giữ chỗ tồn kho của đơn không còn hợp lệ.");

                    var before = variant.StockQuantity;
                    variant.ReservedQuantity -= quantity;
                    variant.StockQuantity -= quantity;
                    variant.UpdatedAt = now;
                    OrderLedger.AddInventoryTransaction(
                        db, variant, "EXPORT_SALE", -quantity, before, order.Id, now, note);
                }
            }
            else if (order.OrderStatus == "Pending" && status == "Cancelled")
            {
                var variants = await LoadOrderVariantsAsync(order, cancellationToken);
                foreach (var group in order.Items.GroupBy(x => x.ProductVariantId))
                {
                    var variant = variants[group.Key];
                    var quantity = group.Sum(x => x.Quantity);
                    if (variant.ReservedQuantity < quantity)
                        throw new InvalidOperationException("Số lượng giữ chỗ của đơn không còn hợp lệ.");

                    variant.ReservedQuantity -= quantity;
                    variant.UpdatedAt = now;
                }
                await RefundIfPaidAsync(order, now, note, cancellationToken);
            }
            else if ((order.OrderStatus is "Confirmed" or "Shipping") && status == "Cancelled")
            {
                var variants = await LoadOrderVariantsAsync(order, cancellationToken);
                foreach (var group in order.Items.GroupBy(x => x.ProductVariantId))
                {
                    var variant = variants[group.Key];
                    var quantity = group.Sum(x => x.Quantity);
                    var before = variant.StockQuantity;
                    variant.StockQuantity += quantity;
                    variant.UpdatedAt = now;
                    OrderLedger.AddInventoryTransaction(
                        db, variant, "RETURN", quantity, before, order.Id, now, note);
                }
                await RefundIfPaidAsync(order, now, note, cancellationToken);
            }
            else if (order.OrderStatus == "Shipping" && status == "Completed")
            {
                if (order.PaymentMethod == "COD")
                    await PaymentMock.RecordReceiptAsync(db, order, now, cancellationToken);
                else if (order.PaymentStatus != "Paid")
                    throw new InvalidOperationException("Đơn hàng phải được thanh toán trước khi hoàn tất.");
            }
            else if (!(order.OrderStatus == "Confirmed" && status == "Shipping"))
            {
                throw new InvalidOperationException(
                    $"Không thể chuyển đơn hàng từ {order.OrderStatus} sang {status}.");
            }

            order.OrderStatus = status;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return order;
        }
        catch (DbUpdateConcurrencyException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new InvalidOperationException("Đơn hàng hoặc tồn kho vừa được cập nhật bởi giao dịch khác.", exception);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<int> ExpirePendingOrdersAsync(
        TimeSpan reservationLifetime,
        CancellationToken cancellationToken = default)
    {
        if (reservationLifetime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(reservationLifetime));

        var expiresAt = DateTime.UtcNow - reservationLifetime;
        var expiredOrderCodes = await db.Orders
            .AsNoTracking()
            .Where(x => x.OrderStatus == "Pending" && x.CreatedAt <= expiresAt)
            .Select(x => x.OrderCode)
            .ToListAsync(cancellationToken);

        var expiredCount = 0;
        foreach (var orderCode in expiredOrderCodes)
        {
            var order = await UpdateOrderStatusAsync(
                orderCode, "Cancelled", "Reservation expired.", "Pending", cancellationToken);
            if (order is not null)
                expiredCount++;
        }

        return expiredCount;
    }

    private async Task<Dictionary<int, ProductVariant>> LoadOrderVariantsAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        var variantIds = order.Items.Select(x => x.ProductVariantId).Distinct().ToArray();
        return await db.ProductVariants
            .Where(x => variantIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
    }

    private async Task RefundIfPaidAsync(
        Order order,
        DateTime now,
        string? note,
        CancellationToken cancellationToken)
    {
        if (order.PaymentStatus != "Paid")
            return;

        var alreadyRefunded = await db.CashFlowLedger.AnyAsync(
            x => x.OrderId == order.Id && x.VoucherType == "PAYMENT" &&
                 x.Category == "CUSTOMER_REFUND" && x.ReferenceType == "Order",
            cancellationToken);
        if (!alreadyRefunded && order.TotalAmount > 0)
        {
            OrderLedger.AddCashFlowEntry(
                db, order, "PAYMENT", "CUSTOMER_REFUND", order.TotalAmount,
                OrderLedger.PaymentMethodFor(order), "Order", order.Id, now, note);
        }
        order.PaymentStatus = "Refunded";
    }

    private static void ValidateCustomer(CreateOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerName) ||
            string.IsNullOrWhiteSpace(request.CustomerPhone) ||
            string.IsNullOrWhiteSpace(request.ShippingAddress))
            throw new InvalidOperationException("Tên, số điện thoại và địa chỉ giao hàng là bắt buộc.");
    }

    private static string NewOrderCode(DateTime now) =>
        $"DH-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..24].ToUpperInvariant();
}
