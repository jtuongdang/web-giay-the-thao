using QuanLyBanGiay.Data;
using QuanLyBanGiay.Models.Entities;

namespace QuanLyBanGiay.Services.Orders;

internal static class OrderLedger
{
    public static void AddInventoryTransaction(
        AppDbContext db,
        ProductVariant variant,
        string transactionType,
        int quantityChange,
        int quantityBefore,
        int orderId,
        DateTime createdAt,
        string? note = null,
        string referenceType = "Order",
        int? referenceId = null)
    {
        db.InventoryTransactions.Add(new InventoryTransaction
        {
            TxCode = NewCode("KHO", createdAt),
            ProductVariantId = variant.Id,
            TxType = transactionType,
            QuantityChange = quantityChange,
            QuantityBefore = quantityBefore,
            QuantityAfter = variant.StockQuantity,
            ReferenceType = referenceType,
            ReferenceId = referenceId ?? orderId,
            CreatedAt = createdAt,
            Note = note
        });
    }

    public static void AddCashFlowEntry(
        AppDbContext db,
        Order order,
        string voucherType,
        string category,
        decimal amount,
        string paymentMethod,
        string referenceType,
        int referenceId,
        DateTime createdAt,
        string? note = null)
    {
        db.CashFlowLedger.Add(new CashFlowLedger
        {
            VoucherCode = NewCode(voucherType == "RECEIPT" ? "PT" : "PC", createdAt),
            VoucherType = voucherType,
            Category = category,
            Amount = amount,
            PaymentMethod = paymentMethod,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            CreatedAt = createdAt,
            Note = note,
            OrderId = order.Id
        });
    }

    public static string PaymentMethodFor(Order order) => order.PaymentMethod switch
    {
        "COD" => "CASH",
        "QR_TRANSFER" => "BANK_TRANSFER",
        "MOCK_CARD" => "MOCK_CARD",
        _ => order.PaymentMethod
    };

    private static string NewCode(string prefix, DateTime createdAt) =>
        $"{prefix}-{createdAt:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}"[..36].ToUpperInvariant();
}
