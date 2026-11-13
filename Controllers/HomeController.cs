using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyBanGiay.Data;
using QuanLyBanGiay.Models;
using QuanLyBanGiay.Models.Entities;

namespace QuanLyBanGiay.Controllers;

public class HomeController(AppDbContext db, ILogger<HomeController> logger) : Controller
{
    public async Task<IActionResult> Index(string? brand, string? search, decimal? minPrice, decimal? maxPrice, string? color, int? size)
    {
        var query = db.Products.AsNoTracking().Include(p => p.Variants)
            .Where(p => p.IsActive && p.Variants.Any(v => v.StockQuantity > v.ReservedQuantity));
        if (!string.IsNullOrWhiteSpace(brand) && !brand.Equals("All", StringComparison.OrdinalIgnoreCase))
            query = query.Where(p => p.Brand == brand);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p => p.Name.Contains(term) || p.Brand.Contains(term) || p.Category.Contains(term));
        }
        if (minPrice.HasValue) query = query.Where(p => p.RetailPrice >= minPrice.Value);
        if (maxPrice.HasValue) query = query.Where(p => p.RetailPrice <= maxPrice.Value);
        if (!string.IsNullOrWhiteSpace(color)) query = query.Where(p => p.Variants.Any(v => v.Color == color && v.StockQuantity > v.ReservedQuantity));
        if (size.HasValue) query = query.Where(p => p.Variants.Any(v => v.Size == size && v.StockQuantity > v.ReservedQuantity));

        var products = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
        var shoes = products.Select(ToShoe).ToList();
        ViewBag.CurrentBrand = brand ?? "All";
        ViewBag.SearchQuery = search ?? "";
        ViewBag.MinPrice = minPrice;
        ViewBag.MaxPrice = maxPrice;
        ViewBag.Color = color;
        ViewBag.Size = size;
        ViewBag.Brands = await db.Products.AsNoTracking().Where(p => p.IsActive).Select(p => p.Brand).Distinct().OrderBy(x => x).ToListAsync();
        ViewBag.Colors = await db.ProductVariants.AsNoTracking().Where(v => v.Product.IsActive && v.StockQuantity > v.ReservedQuantity).Select(v => v.Color).Distinct().OrderBy(x => x).ToListAsync();
        return View(shoes);
    }

    public async Task<IActionResult> Details(int id)
    {
        var product = await db.Products.AsNoTracking().Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
        if (product is null) return NotFound();

        ViewBag.Variants = product.Variants.OrderBy(v => v.Size).ThenBy(v => v.Color)
            .Select(v => new ProductVariantOption(v.Id, v.Size, v.Color, Math.Max(0, v.StockQuantity - v.ReservedQuantity), v.ImageUrl)).ToList();
        ViewBag.RelatedShoes = await db.Products.AsNoTracking().Include(p => p.Variants)
            .Where(p => p.IsActive && p.Brand == product.Brand && p.Id != id && p.Variants.Any(v => v.StockQuantity > v.ReservedQuantity))
            .OrderByDescending(p => p.CreatedAt).Take(4).Select(p => ToShoe(p)).ToListAsync();
        return View(ToShoe(product));
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        logger.LogError("Storefront request failed. RequestId: {RequestId}", Activity.Current?.Id ?? HttpContext.TraceIdentifier);
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private static ShoeItem ToShoe(Product p) => new()
    {
        Id = p.Id, Name = p.Name, Brand = p.Brand, Category = p.Category, Price = p.RetailPrice,
        OriginalPrice = p.OriginalPrice, ImageUrl = p.ImageUrl, Rating = p.Rating,
        IsNew = p.IsNew, IsBestSeller = p.IsBestSeller,
        AvailableSizes = p.Variants.Where(v => v.StockQuantity > v.ReservedQuantity).Select(v => v.Size).Distinct().OrderBy(x => x).ToList()
    };
}

public sealed record ProductVariantOption(int Id, int Size, string Color, int AvailableQuantity, string? ImageUrl);
