namespace QuanLyBanGiay.Models.DTOs;

/// <summary>Customer and line-item information submitted at checkout.</summary>
public sealed record CreateOrderRequest
{
    /// <summary>Customer's full name.</summary>
    public string CustomerName { get; init; } = string.Empty;

    /// <summary>Customer's contact phone number.</summary>
    public string CustomerPhone { get; init; } = string.Empty;

    /// <summary>Delivery address.</summary>
    public string ShippingAddress { get; init; } = string.Empty;

    /// <summary>Voucher code to apply, when supplied.</summary>
    public string? VoucherCode { get; init; }

    /// <summary>Selected payment method, such as COD or QR_TRANSFER.</summary>
    public string PaymentMethod { get; init; } = string.Empty;

    /// <summary>Items requested for purchase.</summary>
    public IReadOnlyList<CreateOrderItemRequest> Items { get; init; } = Array.Empty<CreateOrderItemRequest>();
}

/// <summary>A product variant and quantity requested at checkout.</summary>
public sealed record CreateOrderItemRequest
{
    /// <summary>Identifier of the size/color variant.</summary>
    public int VariantId { get; init; }

    /// <summary>Quantity requested.</summary>
    public int Quantity { get; init; }
}

/// <summary>Order identifier, payable amount, and optional simulated QR payment data.</summary>
public sealed record OrderCreatedResponse
{
    /// <summary>Human-readable order code.</summary>
    public string OrderCode { get; init; } = string.Empty;

    /// <summary>Total amount payable; VAT is not added by this contract.</summary>
    public decimal TotalAmount { get; init; }

    /// <summary>QR payment payload or URL when QR transfer is selected.</summary>
    public string? QrCodeData { get; init; }
}

/// <summary>Administrative request to simulate a payment result for an order.</summary>
public sealed record MockSimulatePaymentRequest
{
    /// <summary>Human-readable order code.</summary>
    public string OrderCode { get; init; } = string.Empty;

    /// <summary>Simulated payment result, such as SUCCESS or FAILED.</summary>
    public string Status { get; init; } = string.Empty;
}

/// <summary>Compact order data for order lists.</summary>
public sealed record OrderSummaryDto
{
    /// <summary>Order identifier.</summary>
    public int Id { get; init; }

    /// <summary>Human-readable order code.</summary>
    public string OrderCode { get; init; } = string.Empty;

    /// <summary>Customer's name.</summary>
    public string CustomerName { get; init; } = string.Empty;

    /// <summary>Customer's phone number.</summary>
    public string CustomerPhone { get; init; } = string.Empty;

    /// <summary>Current order state.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Total amount payable.</summary>
    public decimal TotalAmount { get; init; }

    /// <summary>Order creation time in UTC.</summary>
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Full order details, including the items and delivery information.</summary>
public sealed record OrderDetailDto
{
    /// <summary>Order identifier.</summary>
    public int Id { get; init; }

    /// <summary>Human-readable order code.</summary>
    public string OrderCode { get; init; } = string.Empty;

    /// <summary>Customer's name.</summary>
    public string CustomerName { get; init; } = string.Empty;

    /// <summary>Customer's phone number.</summary>
    public string CustomerPhone { get; init; } = string.Empty;

    /// <summary>Delivery address.</summary>
    public string ShippingAddress { get; init; } = string.Empty;

    /// <summary>Order payment method.</summary>
    public string PaymentMethod { get; init; } = string.Empty;

    /// <summary>Payment state.</summary>
    public string PaymentStatus { get; init; } = string.Empty;

    /// <summary>Applied voucher code, if any.</summary>
    public string? VoucherCode { get; init; }

    /// <summary>Discount deducted from the subtotal.</summary>
    public decimal DiscountAmount { get; init; }

    /// <summary>Total amount payable.</summary>
    public decimal TotalAmount { get; init; }

    /// <summary>Current order state.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Order creation time in UTC.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Items included in the order.</summary>
    public IReadOnlyList<OrderItemDto> Items { get; init; } = Array.Empty<OrderItemDto>();
}

/// <summary>A product variant and its price/quantity in an order.</summary>
public sealed record OrderItemDto
{
    /// <summary>Order line identifier.</summary>
    public int Id { get; init; }

    /// <summary>Product variant identifier.</summary>
    public int VariantId { get; init; }

    /// <summary>Product name at the time of ordering.</summary>
    public string ProductName { get; init; } = string.Empty;

    /// <summary>Variant size.</summary>
    public int Size { get; init; }

    /// <summary>Variant color.</summary>
    public string Color { get; init; } = string.Empty;

    /// <summary>Unit price at the time of ordering.</summary>
    public decimal UnitPrice { get; init; }

    /// <summary>Quantity ordered.</summary>
    public int Quantity { get; init; }

    /// <summary>Line total.</summary>
    public decimal LineTotal { get; init; }
}

/// <summary>Administrative request to move an order to a new state.</summary>
public sealed record UpdateOrderStatusRequest
{
    /// <summary>Target order state, such as Confirmed, Shipping, Completed, or Cancelled.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Optional internal note explaining the transition.</summary>
    public string? Note { get; init; }
}
