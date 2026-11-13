namespace QuanLyBanGiay.Models.DTOs;

/// <summary>Query filters and pagination options for product listings.</summary>
public sealed record ProductFilterQuery
{
    /// <summary>Filter by brand name.</summary>
    public string? Brand { get; init; }

    /// <summary>Filter by shoe size.</summary>
    public int? Size { get; init; }

    /// <summary>Filter by color.</summary>
    public string? Color { get; init; }

    /// <summary>Search term matched against product names or SKUs.</summary>
    public string? Search { get; init; }

    /// <summary>Inclusive minimum retail price.</summary>
    public decimal? MinPrice { get; init; }

    /// <summary>Inclusive maximum retail price.</summary>
    public decimal? MaxPrice { get; init; }

    /// <summary>Zero-based page index.</summary>
    public int PageIndex { get; init; }

    /// <summary>Maximum number of products per page.</summary>
    public int PageSize { get; init; } = 20;
}

/// <summary>Compact product information for storefront listings.</summary>
public sealed record ProductSummaryDto
{
    /// <summary>Product identifier.</summary>
    public int Id { get; init; }

    /// <summary>Stock keeping unit.</summary>
    public string Sku { get; init; } = string.Empty;

    /// <summary>Product name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Brand name.</summary>
    public string Brand { get; init; } = string.Empty;

    /// <summary>Product category.</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>Current retail price.</summary>
    public decimal RetailPrice { get; init; }

    /// <summary>Original price before discount, when applicable.</summary>
    public decimal? OriginalPrice { get; init; }

    /// <summary>Primary product image URL.</summary>
    public string? ImageUrl { get; init; }
}

/// <summary>Full storefront product details, including available variants.</summary>
public sealed record ProductDetailDto
{
    /// <summary>Product identifier.</summary>
    public int Id { get; init; }

    /// <summary>Stock keeping unit.</summary>
    public string Sku { get; init; } = string.Empty;

    /// <summary>Product name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Brand name.</summary>
    public string Brand { get; init; } = string.Empty;

    /// <summary>Product category.</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>Current retail price.</summary>
    public decimal RetailPrice { get; init; }

    /// <summary>Original price before discount, when applicable.</summary>
    public decimal? OriginalPrice { get; init; }

    /// <summary>Primary product image URL.</summary>
    public string? ImageUrl { get; init; }

    /// <summary>Size and color variants with available stock quantities.</summary>
    public IReadOnlyList<ProductVariantDto> Variants { get; init; } = Array.Empty<ProductVariantDto>();
}

/// <summary>Size/color product option and its sellable stock.</summary>
public sealed record ProductVariantDto
{
    /// <summary>Variant identifier.</summary>
    public int VariantId { get; init; }

    /// <summary>Shoe size.</summary>
    public int Size { get; init; }

    /// <summary>Variant color.</summary>
    public string Color { get; init; } = string.Empty;

    /// <summary>Variant-specific image URL, when available.</summary>
    public string? ImageUrl { get; init; }

    /// <summary>Whether the variant can currently be purchased.</summary>
    public bool InStock { get; init; }

    /// <summary>Quantity available after reservations.</summary>
    public int AvailableQuantity { get; init; }
}
