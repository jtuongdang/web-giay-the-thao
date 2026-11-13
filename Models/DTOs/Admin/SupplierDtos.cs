namespace QuanLyBanGiay.Models.DTOs;

/// <summary>Supplier contact details and outstanding payable balance.</summary>
public sealed record SupplierDto
{
    /// <summary>Supplier identifier.</summary>
    public int Id { get; init; }

    /// <summary>Supplier or business name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Primary contact name.</summary>
    public string? ContactName { get; init; }

    /// <summary>Supplier phone number.</summary>
    public string? PhoneNumber { get; init; }

    /// <summary>Supplier email address.</summary>
    public string? Email { get; init; }

    /// <summary>Supplier mailing address.</summary>
    public string? Address { get; init; }

    /// <summary>Outstanding amount owed to the supplier.</summary>
    public decimal CurrentDebt { get; init; }

    /// <summary>Whether the supplier is active.</summary>
    public bool IsActive { get; init; }
}

/// <summary>Supplier contact information submitted for creation.</summary>
public sealed record CreateSupplierRequest
{
    /// <summary>Supplier or business name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Primary contact name.</summary>
    public string? ContactName { get; init; }

    /// <summary>Supplier phone number.</summary>
    public string? PhoneNumber { get; init; }

    /// <summary>Supplier email address.</summary>
    public string? Email { get; init; }

    /// <summary>Supplier mailing address.</summary>
    public string? Address { get; init; }
}

/// <summary>Payment submitted against a supplier's outstanding balance.</summary>
public sealed record CreateSupplierPaymentRequest
{
    /// <summary>Amount paid to the supplier.</summary>
    public decimal Amount { get; init; }

    /// <summary>Payment method, such as cash or bank transfer.</summary>
    public string PaymentMethod { get; init; } = string.Empty;

    /// <summary>Optional purchase order to which the payment applies.</summary>
    public int? PurchaseOrderId { get; init; }

    /// <summary>Optional external payment reference.</summary>
    public string? Reference { get; init; }

    /// <summary>Optional payment note.</summary>
    public string? Note { get; init; }
}

/// <summary>Recorded payment made to a supplier.</summary>
public sealed record SupplierPaymentDto
{
    /// <summary>Supplier payment identifier.</summary>
    public int Id { get; init; }

    /// <summary>Supplier identifier.</summary>
    public int SupplierId { get; init; }

    /// <summary>Optional purchase order paid by this transaction.</summary>
    public int? PurchaseOrderId { get; init; }

    /// <summary>Amount paid.</summary>
    public decimal Amount { get; init; }

    /// <summary>Payment method.</summary>
    public string PaymentMethod { get; init; } = string.Empty;

    /// <summary>External payment reference, when provided.</summary>
    public string? Reference { get; init; }

    /// <summary>Payment time in UTC.</summary>
    public DateTimeOffset PaidAt { get; init; }
}
