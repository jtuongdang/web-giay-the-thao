using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QuanLyBanGiay.Models;
using QuanLyBanGiay.Models.Entities;

namespace QuanLyBanGiay.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(AppDbContext db)
    {
        var hasher = new PasswordHasher<User>();
        var defaultUsers = new[]
        {
            (new User { Username = "admin", Email = "admin@quanlybangiay.local", FullName = "Administrator", Role = "Admin", IsActive = true }, "Admin@123"),
            (new User { Username = "staff", Email = "staff@quanlybangiay.local", FullName = "Store Staff", Role = "Staff", IsActive = true }, "Staff@123"),
            (new User { Username = "customer", Email = "customer@quanlybangiay.local", FullName = "Sample Customer", Role = "Customer", IsActive = true }, "Customer@123")
        };

        foreach (var (user, password) in defaultUsers)
        {
            if (await db.Users.AnyAsync(x => x.Username == user.Username)) continue;
            user.CreatedAt = DateTime.UtcNow;
            user.PasswordHash = hasher.HashPassword(user, password);
            db.Users.Add(user);
        }
        await db.SaveChangesAsync();

        if (!await db.Suppliers.AnyAsync(x => x.Code == "NIKE-VN"))
            db.Suppliers.Add(new Supplier { Code = "NIKE-VN", Name = "Nike VN Distributor", CurrentDebt = 0 });
        if (!await db.Suppliers.AnyAsync(x => x.Code == "ADIDAS-OFFICIAL"))
            db.Suppliers.Add(new Supplier { Code = "ADIDAS-OFFICIAL", Name = "Adidas Official Supply", CurrentDebt = 0 });

        var now = DateTime.UtcNow;
        if (!await db.Vouchers.AnyAsync(x => x.Code == "SNEAKER10"))
            db.Vouchers.Add(new Voucher
            {
                Code = "SNEAKER10", DiscountType = "PERCENT", DiscountValue = 10,
                MinOrderAmount = 0, MaxDiscountAmount = 500000, UsageLimit = 1000,
                UsedCount = 0, StartAt = now.AddYears(-1), EndAt = now.AddYears(10), IsActive = true
            });
        if (!await db.Vouchers.AnyAsync(x => x.Code == "WELCOME100K"))
        {
            db.Vouchers.Add(new Voucher
            {
                Code = "WELCOME100K", DiscountType = "FIXED_AMOUNT", DiscountValue = 100000,
                MinOrderAmount = 1000000, UsageLimit = 1000, UsedCount = 0,
                StartAt = now.AddYears(-1), EndAt = now.AddYears(10), IsActive = true
            });
        }

        await db.SaveChangesAsync();

        var adminId = await db.Users.Where(x => x.Username == "admin").Select(x => x.Id).FirstOrDefaultAsync();
        var seedDate = DateTime.UtcNow;
        foreach (var shoe in ShoeDataService.GetAll())
        {
            var sku = $"SHOE-{shoe.Id:000}";
            var product = await db.Products.Include(x => x.Variants).SingleOrDefaultAsync(x => x.Sku == sku);
            if (product is null)
            {
                product = new Product
                {
                    Sku = sku,
                    Name = shoe.Name,
                    Brand = shoe.Brand,
                    Category = shoe.Category,
                    RetailPrice = shoe.Price,
                    OriginalPrice = shoe.OriginalPrice,
                    ImageUrl = shoe.ImageUrl,
                    Rating = shoe.Rating,
                    IsNew = shoe.IsNew,
                    IsBestSeller = shoe.IsBestSeller,
                    IsActive = true,
                    CreatedAt = seedDate
                };
                db.Products.Add(product);
                await db.SaveChangesAsync();
            }

            foreach (var size in shoe.AvailableSizes)
            {
                const string color = "Default";
                var variantSku = $"{sku}-SZ{size}";
                var variant = await db.ProductVariants.SingleOrDefaultAsync(x => x.VariantSku == variantSku);
                if (variant is null)
                {
                    variant = new ProductVariant
                    {
                        ProductId = product.Id,
                        Size = size,
                        Color = color,
                        VariantSku = variantSku,
                        ImageUrl = shoe.ImageUrl,
                        CostPrice = decimal.Round(shoe.Price * 0.70m, 2),
                        StockQuantity = 15,
                        ReservedQuantity = 0,
                        MinStockAlert = 3,
                        UpdatedAt = seedDate
                    };
                    db.ProductVariants.Add(variant);
                    await db.SaveChangesAsync();
                }

                var txCode = $"INIT-{variantSku}";
                if (!await db.InventoryTransactions.AnyAsync(x => x.TxCode == txCode))
                {
                    db.InventoryTransactions.Add(new InventoryTransaction
                    {
                        TxCode = txCode,
                        ProductVariantId = variant.Id,
                        TxType = "IMPORT",
                        QuantityChange = 15,
                        QuantityBefore = 0,
                        QuantityAfter = 15,
                        ReferenceType = "Manual",
                        ReferenceId = variant.Id,
                        CreatedByUserId = adminId == 0 ? null : adminId,
                        CreatedAt = seedDate,
                        Note = "Initial inventory seed"
                    });
                }
            }
        }

        await db.SaveChangesAsync();
    }
}
