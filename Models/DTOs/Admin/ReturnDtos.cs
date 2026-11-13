namespace QuanLyBanGiay.Models.DTOs;

/// <summary>Return or exchange request for a completed order.</summary>
public sealed record CreateOrderReturnRequest
{
    /// <summary>Order identifier to return or exchange.</summary>
    public int OrderId { get; init; }

    /// <summary>Return action, such as EXCHANGE or REFUND.</summary>
    public string ReturnType { get; init; } = string.Empty;

    /// <summary>Reason for the return or exchange.</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>Items included in the return request.</summary>
    public IReadOnlyList<CreateOrderReturnItemRequest> Items { get; init; } =
        Array.Empty<CreateOrderReturnItemRequest>();
}

/// <summary>Order line and quantity included in a return or exchange.</summary>
public sealed record CreateOrderReturnItemRequest
{
    /// <summary>Order line identifier being returned.</summary>
    public int OrderItemId { get; init; }

    /// <summary>Quantity being returned.</summary>
    public int Quantity { get; init; }

    /// <summary>Replacement variant identifier for an exchange, when applicable.</summary>
    public int? ReplacementVariantId { get; init; }
}

/// <summary>Recorded order return or exchange details.</summary>
public sealed record OrderReturnDto
{
    /// <summary>Return identifier.</summary>
    public int Id { get; init; }

    /// <summary>Associated order identifier.</summary>
    public int OrderId { get; init; }

    /// <summary>Associated human-readable order code.</summary>
    public string OrderCode { get; init; } = string.Empty;

    /// <summary>Return action, such as EXCHANGE or REFUND.</summary>
    public string ReturnType { get; init; } = string.Empty;

    /// <summary>Reason for the return or exchange.</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>Current processing state.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Refund amount, when applicable.</summary>
    public decimal RefundAmount { get; init; }

    /// <summary>Request creation time in UTC.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Items included in this return or exchange.</summary>
    public IReadOnlyList<CreateOrderReturnItemRequest> Items { get; init; } =
        Array.Empty<CreateOrderReturnItemRequest>();
}
