namespace QuanLyBanGiay.Models.DTOs;

/// <summary>Inventory levels and product details for a size/color variant.</summary>
public sealed record InventoryItemDto
{
    /// <summary>Variant identifier.</summary>
    public int VariantId { get; init; }

    /// <summary>Product identifier.</summary>
    public int ProductId { get; init; }

    /// <summary>Product name.</summary>
    public string ProductName { get; init; } = string.Empty;

    /// <summary>Product stock keeping unit.</summary>
    public string Sku { get; init; } = string.Empty;

    /// <summary>Variant size.</summary>
    public int Size { get; init; }

    /// <summary>Variant color.</summary>
    public string Color { get; init; } = string.Empty;

    /// <summary>Physical quantity currently in stock.</summary>
    public int StockQuantity { get; init; }

    /// <summary>Quantity reserved for unfulfilled orders.</summary>
    public int ReservedQuantity { get; init; }

    /// <summary>Sellable quantity after reservations.</summary>
    public int AvailableQuantity { get; init; }

    /// <summary>Current unit cost.</summary>
    public decimal CostPrice { get; init; }

    /// <summary>Whether the item is below its configured low-stock threshold.</summary>
    public bool IsLowStock { get; init; }
}

/// <summary>A recorded change to inventory quantities.</summary>
public sealed record InventoryTransactionDto
{
    /// <summary>Inventory transaction identifier.</summary>
    public int Id { get; init; }

    /// <summary>Variant affected by the transaction.</summary>
    public int VariantId { get; init; }

    /// <summary>Product name for the affected variant.</summary>
    public string ProductName { get; init; } = string.Empty;

    /// <summary>Transaction kind, such as IMPORT or ADJUSTMENT.</summary>
    public string TransactionType { get; init; } = string.Empty;

    /// <summary>Signed quantity change.</summary>
    public int QuantityChange { get; init; }

    /// <summary>Physical stock quantity after the transaction.</summary>
    public int StockAfter { get; init; }

    /// <summary>Optional transaction description or reason.</summary>
    public string? Note { get; init; }

    /// <summary>Transaction time in UTC.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>User who recorded the transaction.</summary>
    public string? CreatedBy { get; init; }
}

/// <summary>Manual stock adjustment for a product variant.</summary>
public sealed record AdjustInventoryRequest
{
    /// <summary>Variant whose stock is being adjusted.</summary>
    public int VariantId { get; init; }

    /// <summary>Signed quantity change; positive adds stock and negative removes stock.</summary>
    public int QuantityChange { get; init; }

    /// <summary>Reason for the stock adjustment.</summary>
    public string Reason { get; init; } = string.Empty;
}

/// <summary>Purchase order header and line items submitted for creation.</summary>
public sealed record CreatePurchaseOrderRequest
{
    /// <summary>Supplier identifier.</summary>
    public int SupplierId { get; init; }

    /// <summary>Optional reference number from the supplier.</summary>
    public string? SupplierReference { get; init; }

    /// <summary>Purchase order items.</summary>
    public IReadOnlyList<CreatePurchaseOrderItemRequest> Items { get; init; } =
        Array.Empty<CreatePurchaseOrderItemRequest>();

    /// <summary>Optional administrative note.</summary>
    public string? Note { get; init; }
}

/// <summary>A variant, quantity, and cost for a purchase order line.</summary>
public sealed record CreatePurchaseOrderItemRequest
{
    /// <summary>Product variant identifier.</summary>
    public int VariantId { get; init; }

    /// <summary>Quantity being ordered.</summary>
    public int Quantity { get; init; }

    /// <summary>Supplier's unit cost.</summary>
    public decimal UnitCost { get; init; }
}

/// <summary>Purchase order and its current receiving/payment state.</summary>
public sealed record PurchaseOrderDto
{
    /// <summary>Purchase order identifier.</summary>
    public int Id { get; init; }

    /// <summary>Purchase order reference code.</summary>
    public string OrderCode { get; init; } = string.Empty;

    /// <summary>Supplier identifier.</summary>
    public int SupplierId { get; init; }

    /// <summary>Supplier name.</summary>
    public string SupplierName { get; init; } = string.Empty;

    /// <summary>Current purchase order state.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Total purchase order value.</summary>
    public decimal TotalAmount { get; init; }

    /// <summary>Amount already paid to the supplier.</summary>
    public decimal PaidAmount { get; init; }

    /// <summary>Purchase order creation time in UTC.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Items included in the purchase order.</summary>
    public IReadOnlyList<CreatePurchaseOrderItemRequest> Items { get; init; } =
        Array.Empty<CreatePurchaseOrderItemRequest>();
}
