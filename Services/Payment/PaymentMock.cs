using Microsoft.EntityFrameworkCore;
using QuanLyBanGiay.Data;
using QuanLyBanGiay.Models.Entities;
using QuanLyBanGiay.Services.Orders;

namespace QuanLyBanGiay.Services.Payment;

public sealed record PaymentSimulationResult(string OrderCode, bool Succeeded, string PaymentStatus);

public sealed class PaymentMock(AppDbContext db)
{
    public async Task<PaymentSimulationResult> SimulateAsync(
        string orderCode,
        string status,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderCode))
            throw new ArgumentException("Mã đơn hàng là bắt buộc.", nameof(orderCode));
        if (string.IsNullOrWhiteSpace(status) ||
            !(status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase) ||
              status.Equals("FAILED", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Kết quả mô phỏng phải là SUCCESS hoặc FAILED.", nameof(status));

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var order = await db.Orders.SingleOrDefaultAsync(
                x => x.OrderCode == orderCode, cancellationToken)
                ?? throw new KeyNotFoundException($"Không tìm thấy đơn hàng {orderCode}.");

            if (order.PaymentMethod is not ("QR_TRANSFER" or "MOCK_CARD"))
                throw new InvalidOperationException("Chỉ hỗ trợ mô phỏng QR_TRANSFER hoặc MOCK_CARD.");
            if (order.OrderStatus == "Cancelled")
                throw new InvalidOperationException("Không thể thanh toán một đơn đã bị hủy.");
            if (order.PaymentStatus == "Refunded")
                throw new InvalidOperationException("Không thể thanh toán lại một đơn đã hoàn tiền.");

            var succeeded = status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase);
            if (succeeded && order.PaymentStatus != "Paid")
            {
                order.PaymentStatus = "Paid";
                await RecordReceiptAsync(db, order, DateTime.UtcNow, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return new PaymentSimulationResult(order.OrderCode, succeeded, order.PaymentStatus);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new InvalidOperationException("Đơn hàng vừa được cập nhật bởi giao dịch khác.", exception);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    internal static async Task RecordReceiptAsync(
        AppDbContext db,
        Order order,
        DateTime createdAt,
        CancellationToken cancellationToken)
    {
        if (await db.CashFlowLedger.AnyAsync(
                x => x.OrderId == order.Id && x.VoucherType == "RECEIPT" &&
                     x.Category == "SALE_REVENUE",
                cancellationToken))
            return;

        order.PaymentStatus = "Paid";
        OrderLedger.AddCashFlowEntry(
            db, order, "RECEIPT", "SALE_REVENUE", order.TotalAmount,
            OrderLedger.PaymentMethodFor(order), "Order", order.Id, createdAt);
    }
}
