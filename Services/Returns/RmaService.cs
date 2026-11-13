using Microsoft.EntityFrameworkCore;
using QuanLyBanGiay.Data;
using QuanLyBanGiay.Models.DTOs;
using QuanLyBanGiay.Models.Entities;
using QuanLyBanGiay.Services.Orders;

namespace QuanLyBanGiay.Services.Returns;

public sealed class RmaService(AppDbContext db)
{
    public async Task<OrderReturnDto> ProcessAsync(
        CreateOrderReturnRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.OrderId <= 0 || request.Items.Count == 0 ||
            string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidOperationException("Đơn, lý do và ít nhất một dòng đổi/trả là bắt buộc.");

        var returnType = request.ReturnType.Trim().ToUpperInvariant();
        if (returnType is not ("EXCHANGE" or "REFUND"))
            throw new InvalidOperationException("Loại xử lý phải là EXCHANGE hoặc REFUND.");
        if (request.Items.Any(x => x.OrderItemId <= 0 || x.Quantity <= 0))
            throw new InvalidOperationException("Dòng đổi/trả có số lượng hoặc mã không hợp lệ.");
        if (returnType == "EXCHANGE" && request.Items.Any(x => x.ReplacementVariantId is null or <= 0))
            throw new InvalidOperationException("Mỗi dòng đổi size cần một biến thể thay thế hợp lệ.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var order = await db.Orders
                .Include(x => x.Items)
                .SingleOrDefaultAsync(x => x.Id == request.OrderId, cancellationToken)
                ?? throw new KeyNotFoundException($"Không tìm thấy đơn hàng {request.OrderId}.");
            if (order.OrderStatus != "Completed" || order.PaymentStatus != "Paid")
                throw new InvalidOperationException("Chỉ đơn đã hoàn tất và thanh toán mới được đổi/trả.");

            var lines = request.Items
                .GroupBy(x => new { x.OrderItemId, x.ReplacementVariantId })
                .Select(g => new { g.Key.OrderItemId, g.Key.ReplacementVariantId, Quantity = g.Sum(x => x.Quantity) })
                .ToArray();
            var orderItemsById = order.Items.ToDictionary(x => x.Id);
            if (lines.Any(x => !orderItemsById.ContainsKey(x.OrderItemId)))
                throw new InvalidOperationException("Một hoặc nhiều dòng không thuộc đơn hàng này.");

            var alreadyReturned = await db.OrderReturnItems
                .Where(x => x.OrderReturn.OrderId == order.Id && x.OrderReturn.Status != "Rejected")
                .GroupBy(x => x.OriginalVariantId)
                .Select(g => new { VariantId = g.Key, Quantity = g.Sum(x => x.Quantity) })
                .ToDictionaryAsync(x => x.VariantId, x => x.Quantity, cancellationToken);

            var requestedByVariant = lines
                .GroupBy(x => orderItemsById[x.OrderItemId].ProductVariantId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
            var purchasedByVariant = order.Items
                .GroupBy(x => x.ProductVariantId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
            foreach (var (variantId, requestedQuantity) in requestedByVariant)
            {
                var totalReturned = alreadyReturned.GetValueOrDefault(variantId);
                if (requestedQuantity + totalReturned > purchasedByVariant[variantId])
                    throw new InvalidOperationException("Số lượng đổi/trả vượt quá số lượng đã mua còn lại.");
            }

            var variantIds = lines.Select(x => orderItemsById[x.OrderItemId].ProductVariantId)
                .Concat(lines.Where(x => x.ReplacementVariantId.HasValue)
                    .Select(x => x.ReplacementVariantId!.Value))
                .Distinct()
                .ToArray();
            var variants = await db.ProductVariants
                .Include(x => x.Product)
                .Where(x => variantIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, cancellationToken);

            var replacementRequirements = lines
                .Where(x => x.ReplacementVariantId.HasValue)
                .GroupBy(x => x.ReplacementVariantId!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
            foreach (var (variantId, quantity) in replacementRequirements)
            {
                if (!variants.TryGetValue(variantId, out var replacement) ||
                    !replacement.Product.IsActive ||
                    replacement.StockQuantity - replacement.ReservedQuantity < quantity)
                    throw new InvalidOperationException($"Biến thể thay thế {variantId} không đủ tồn kho khả dụng.");
            }

            var now = DateTime.UtcNow;
            var orderReturn = new OrderReturn
            {
                ReturnCode = $"RT-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..24].ToUpperInvariant(),
                OrderId = order.Id,
                ReturnType = returnType,
                Reason = request.Reason.Trim(),
                Status = "Completed",
                CreatedAt = now,
                CompletedAt = now
            };
            db.OrderReturns.Add(orderReturn);
            await db.SaveChangesAsync(cancellationToken);

            decimal refundAmount = 0m;
            decimal exchangeDifference = 0m;
            var responseItems = new List<CreateOrderReturnItemRequest>();
            foreach (var line in lines)
            {
                var orderItem = orderItemsById[line.OrderItemId];
                var originalVariant = variants[orderItem.ProductVariantId];
                var returnedGross = orderItem.UnitPrice * line.Quantity;

                if (returnType == "REFUND")
                {
                    if (request.Items.Any(x => x.OrderItemId == line.OrderItemId &&
                                               x.ReplacementVariantId.HasValue))
                        throw new InvalidOperationException("Dòng hoàn tiền không được có biến thể thay thế.");

                    var netFactor = order.Subtotal == 0
                        ? 1m
                        : (order.Subtotal - order.DiscountAmount) / order.Subtotal;
                    refundAmount += decimal.Round(returnedGross * netFactor, 2);
                }
                else
                {
                    var replacement = variants[line.ReplacementVariantId!.Value];
                    if (replacement.Id == originalVariant.Id)
                        throw new InvalidOperationException("Biến thể thay thế phải khác biến thể được trả.");
                    exchangeDifference += (replacement.Product.RetailPrice - orderItem.UnitPrice) * line.Quantity;
                    replacement.StockQuantity -= line.Quantity;
                    replacement.UpdatedAt = now;
                    OrderLedger.AddInventoryTransaction(
                        db, replacement, "EXCHANGE_OUT", -line.Quantity, replacement.StockQuantity + line.Quantity,
                        order.Id, now, $"RMA {orderReturn.ReturnCode}", "OrderReturn", orderReturn.Id);
                }

                var before = originalVariant.StockQuantity;
                originalVariant.StockQuantity += line.Quantity;
                originalVariant.UpdatedAt = now;
                OrderLedger.AddInventoryTransaction(
                    db, originalVariant, "RETURN", line.Quantity, before, order.Id, now,
                    $"RMA {orderReturn.ReturnCode}", "OrderReturn", orderReturn.Id);

                orderReturn.Items.Add(new OrderReturnItem
                {
                    OriginalVariantId = originalVariant.Id,
                    ExchangeVariantId = line.ReplacementVariantId,
                    Quantity = line.Quantity,
                    RefundUnitPrice = orderItem.UnitPrice,
                    IsResellable = true
                });
                responseItems.Add(new CreateOrderReturnItemRequest
                {
                    OrderItemId = line.OrderItemId,
                    Quantity = line.Quantity,
                    ReplacementVariantId = line.ReplacementVariantId
                });
            }

            await db.SaveChangesAsync(cancellationToken);

            if (returnType == "REFUND")
            {
                orderReturn.RefundAmount = refundAmount;
                if (refundAmount > 0)
                {
                    OrderLedger.AddCashFlowEntry(
                        db, order, "PAYMENT", "CUSTOMER_REFUND", refundAmount,
                        OrderLedger.PaymentMethodFor(order), "OrderReturn", orderReturn.Id, now, orderReturn.ReturnCode);
                    order.PaymentStatus = "Refunded";
                }
            }
            else if (exchangeDifference != 0)
            {
                var isReceipt = exchangeDifference > 0;
                orderReturn.RefundAmount = isReceipt ? 0 : -exchangeDifference;
                OrderLedger.AddCashFlowEntry(
                    db, order, isReceipt ? "RECEIPT" : "PAYMENT",
                    isReceipt ? "SALE_REVENUE" : "CUSTOMER_REFUND",
                    Math.Abs(exchangeDifference), OrderLedger.PaymentMethodFor(order),
                    "OrderReturn", orderReturn.Id, now, orderReturn.ReturnCode);
                if (!isReceipt)
                    order.PaymentStatus = "Refunded";
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ToDto(order, orderReturn, responseItems);
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

    private static OrderReturnDto ToDto(
        Order order,
        OrderReturn orderReturn,
        IReadOnlyList<CreateOrderReturnItemRequest> items) => new()
    {
        Id = orderReturn.Id,
        OrderId = order.Id,
        OrderCode = order.OrderCode,
        ReturnType = orderReturn.ReturnType,
        Reason = orderReturn.Reason,
        Status = orderReturn.Status,
        RefundAmount = orderReturn.RefundAmount,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(orderReturn.CreatedAt, DateTimeKind.Utc)),
        Items = items
    };
}
