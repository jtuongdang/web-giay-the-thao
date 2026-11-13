namespace QuanLyBanGiay.Models.Entities;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Role { get; set; } = "Customer";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}

public class Product
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal RetailPrice { get; set; }
    public decimal? OriginalPrice { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public double Rating { get; set; }
    public bool IsNew { get; set; }
    public bool IsBestSeller { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
}

public class ProductVariant
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Size { get; set; }
    public string Color { get; set; } = string.Empty;
    public string VariantSku { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal CostPrice { get; set; }
    public int StockQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int MinStockAlert { get; set; }
    public byte[]? RowVersion { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<PurchaseOrderItem> PurchaseOrderItems { get; set; } = new List<PurchaseOrderItem>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public ICollection<OrderReturnItem> OriginalReturnItems { get; set; } = new List<OrderReturnItem>();
    public ICollection<OrderReturnItem> ExchangeReturnItems { get; set; } = new List<OrderReturnItem>();
    public ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
}

public class Voucher
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DiscountType { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public decimal MinOrderAmount { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public int UsageLimit { get; set; }
    public int UsedCount { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}

public class Supplier
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public decimal CurrentDebt { get; set; }
    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
    public ICollection<SupplierPayment> SupplierPayments { get; set; } = new List<SupplierPayment>();
}

public class PurchaseOrder
{
    public int Id { get; set; }
    public string PoCode { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public string PaymentStatus { get; set; } = "Unpaid";
    public string Status { get; set; } = "Draft";
    public DateTime OrderDate { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
    public ICollection<SupplierPayment> SupplierPayments { get; set; } = new List<SupplierPayment>();
}

public class PurchaseOrderItem
{
    public int Id { get; set; }
    public int PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public int ProductVariantId { get; set; }
    public ProductVariant ProductVariant { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
}

public class SupplierPayment
{
    public int Id { get; set; }
    public string PaymentCode { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public int? PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public DateTime PaidAt { get; set; }
    public string? Note { get; set; }
}

public class Order
{
    public int Id { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public User? User { get; set; }
    public int? VoucherId { get; set; }
    public Voucher? Voucher { get; set; }
    public string? VoucherCode { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = "Pending";
    public string OrderStatus { get; set; } = "Pending";
    public byte[]? RowVersion { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<OrderReturn> Returns { get; set; } = new List<OrderReturn>();
    public ICollection<CashFlowLedger> CashFlowEntries { get; set; } = new List<CashFlowLedger>();
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int ProductVariantId { get; set; }
    public ProductVariant ProductVariant { get; set; } = null!;
    public string ProductName { get; set; } = string.Empty;
    public int Size { get; set; }
    public string Color { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal UnitCost { get; set; }
    public int Quantity { get; set; }
    public decimal TotalPrice { get; set; }
}

public class OrderReturn
{
    public int Id { get; set; }
    public string ReturnCode { get; set; } = string.Empty;
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public string ReturnType { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public decimal RefundAmount { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public ICollection<OrderReturnItem> Items { get; set; } = new List<OrderReturnItem>();
}

public class OrderReturnItem
{
    public int Id { get; set; }
    public int OrderReturnId { get; set; }
    public OrderReturn OrderReturn { get; set; } = null!;
    public int OriginalVariantId { get; set; }
    public ProductVariant OriginalVariant { get; set; } = null!;
    public int? ExchangeVariantId { get; set; }
    public ProductVariant? ExchangeVariant { get; set; }
    public int Quantity { get; set; }
    public decimal RefundUnitPrice { get; set; }
    public bool IsResellable { get; set; }
}

public class InventoryTransaction
{
    public int Id { get; set; }
    public string TxCode { get; set; } = string.Empty;
    public int ProductVariantId { get; set; }
    public ProductVariant ProductVariant { get; set; } = null!;
    public string TxType { get; set; } = string.Empty;
    public int QuantityChange { get; set; }
    public int QuantityBefore { get; set; }
    public int QuantityAfter { get; set; }
    public string ReferenceType { get; set; } = string.Empty;
    public int ReferenceId { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Note { get; set; }
}

public class CashFlowLedger
{
    public int Id { get; set; }
    public string VoucherCode { get; set; } = string.Empty;
    public string VoucherType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string ReferenceType { get; set; } = string.Empty;
    public int ReferenceId { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Note { get; set; }
    public int? OrderId { get; set; }
    public Order? Order { get; set; }
    public int? PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }
    public int? SupplierPaymentId { get; set; }
    public SupplierPayment? SupplierPayment { get; set; }
}
