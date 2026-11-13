namespace QuanLyBanGiay.Models.DTOs;

/// <summary>Voucher code and order subtotal submitted for validation.</summary>
public sealed record ValidateVoucherRequest
{
    /// <summary>Voucher code to validate.</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Order subtotal before applying the voucher.</summary>
    public decimal Subtotal { get; init; }
}

/// <summary>Result of validating a voucher against an order subtotal.</summary>
public sealed record ValidateVoucherResponse
{
    /// <summary>Whether the voucher can be applied.</summary>
    public bool IsValid { get; init; }

    /// <summary>Discount amount, or zero when invalid.</summary>
    public decimal DiscountAmount { get; init; }

    /// <summary>Subtotal after the discount is applied.</summary>
    public decimal FinalSubtotal { get; init; }

    /// <summary>Explanation of the validation result.</summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>Voucher details submitted when creating a promotion.</summary>
public sealed record CreateVoucherRequest
{
    /// <summary>Unique voucher code.</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Optional description of the promotion.</summary>
    public string? Description { get; init; }

    /// <summary>Discount kind, such as Percentage or FixedAmount.</summary>
    public string DiscountType { get; init; } = string.Empty;

    /// <summary>Percentage or fixed amount to discount.</summary>
    public decimal DiscountValue { get; init; }

    /// <summary>Minimum order subtotal required to use the voucher.</summary>
    public decimal MinimumOrderAmount { get; init; }

    /// <summary>Maximum discount for percentage promotions.</summary>
    public decimal? MaximumDiscountAmount { get; init; }

    /// <summary>Maximum number of redemptions, when limited.</summary>
    public int? UsageLimit { get; init; }

    /// <summary>Promotion start time in UTC.</summary>
    public DateTimeOffset StartsAt { get; init; }

    /// <summary>Promotion end time in UTC.</summary>
    public DateTimeOffset EndsAt { get; init; }

    /// <summary>Whether the voucher is enabled.</summary>
    public bool IsActive { get; init; } = true;
}

/// <summary>Fields that can be changed when updating a voucher.</summary>
public sealed record UpdateVoucherRequest
{
    /// <summary>Optional updated description.</summary>
    public string? Description { get; init; }

    /// <summary>Optional updated discount kind, such as Percentage or FixedAmount.</summary>
    public string? DiscountType { get; init; }

    /// <summary>Optional updated percentage or fixed discount amount.</summary>
    public decimal? DiscountValue { get; init; }

    /// <summary>Optional updated minimum order subtotal.</summary>
    public decimal? MinimumOrderAmount { get; init; }

    /// <summary>Optional updated maximum discount for percentage promotions.</summary>
    public decimal? MaximumDiscountAmount { get; init; }

    /// <summary>Optional updated redemption limit.</summary>
    public int? UsageLimit { get; init; }

    /// <summary>Optional updated promotion start time in UTC.</summary>
    public DateTimeOffset? StartsAt { get; init; }

    /// <summary>Optional updated promotion end time in UTC.</summary>
    public DateTimeOffset? EndsAt { get; init; }

    /// <summary>Optional enabled/disabled state.</summary>
    public bool? IsActive { get; init; }
}

/// <summary>Voucher details returned by administrative endpoints.</summary>
public sealed record VoucherDto
{
    /// <summary>Voucher identifier.</summary>
    public int Id { get; init; }

    /// <summary>Unique voucher code.</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Promotion description.</summary>
    public string? Description { get; init; }

    /// <summary>Discount kind.</summary>
    public string DiscountType { get; init; } = string.Empty;

    /// <summary>Percentage or fixed amount discounted.</summary>
    public decimal DiscountValue { get; init; }

    /// <summary>Minimum order subtotal required to use this voucher.</summary>
    public decimal MinimumOrderAmount { get; init; }

    /// <summary>Maximum discount for percentage promotions.</summary>
    public decimal? MaximumDiscountAmount { get; init; }

    /// <summary>Maximum number of redemptions, when limited.</summary>
    public int? UsageLimit { get; init; }

    /// <summary>Number of times the voucher has been redeemed.</summary>
    public int UsageCount { get; init; }

    /// <summary>Promotion start time in UTC.</summary>
    public DateTimeOffset StartsAt { get; init; }

    /// <summary>Promotion end time in UTC.</summary>
    public DateTimeOffset EndsAt { get; init; }

    /// <summary>Whether the voucher is enabled.</summary>
    public bool IsActive { get; init; }
}
