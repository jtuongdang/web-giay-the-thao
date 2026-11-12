namespace QuanLyBanGiay.Models;

public static class ShoeDataService
{
    private static readonly List<ShoeItem> _shoes = new()
    {
        new ShoeItem
        {
            Id = 1,
            Name = "Nike Air Force 1 '07",
            Brand = "Nike",
            Category = "Thời trang",
            Price = 2929000,
            OriginalPrice = 3200000,
            ImageUrl = "https://images.unsplash.com/photo-1595950653106-6c9ebd614d3a?w=800&auto=format&fit=crop&q=80",
            AvailableSizes = new() { 39, 40, 41, 42, 43 },
            Rating = 4.9,
            ReviewCount = 124,
            IsNew = true,
            IsBestSeller = true
        },
        new ShoeItem
        {
            Id = 2,
            Name = "Air Jordan 1 Retro High OG",
            Brand = "Jordan",
            Category = "Bóng rổ",
            Price = 4999000,
            OriginalPrice = 5500000,
            ImageUrl = "https://images.unsplash.com/photo-1552346154-21d32810aba3?w=800&auto=format&fit=crop&q=80",
            AvailableSizes = new() { 40, 41, 42, 43, 44 },
            Rating = 5.0,
            ReviewCount = 210,
            IsNew = true,
            IsBestSeller = true
        },
        new ShoeItem
        {
            Id = 3,
            Name = "Adidas Ultraboost Light",
            Brand = "Adidas",
            Category = "Chạy bộ",
            Price = 3800000,
            OriginalPrice = 4500000,
            ImageUrl = "https://images.unsplash.com/photo-1587563871167-1ee9c731aefb?w=800&auto=format&fit=crop&q=80",
            AvailableSizes = new() { 39, 40, 41, 42 },
            Rating = 4.8,
            ReviewCount = 89,
            IsNew = false,
            IsBestSeller = true
        },
        new ShoeItem
        {
            Id = 4,
            Name = "Puma Deviate Nitro 2",
            Brand = "Puma",
            Category = "Chạy bộ",
            Price = 3290000,
            OriginalPrice = null,
            ImageUrl = "https://images.unsplash.com/photo-1608231387042-66d1773070a5?w=800&auto=format&fit=crop&q=80",
            AvailableSizes = new() { 40, 41, 42, 43 },
            Rating = 4.7,
            ReviewCount = 56,
            IsNew = true,
            IsBestSeller = false
        },
        new ShoeItem
        {
            Id = 5,
            Name = "Nike Pegasus 41 Road Running",
            Brand = "Nike",
            Category = "Chạy bộ",
            Price = 3590000,
            OriginalPrice = 3890000,
            ImageUrl = "https://images.unsplash.com/photo-1542291026-7eec264c27ff?w=800&auto=format&fit=crop&q=80",
            AvailableSizes = new() { 38, 39, 40, 41, 42 },
            Rating = 4.9,
            ReviewCount = 175,
            IsNew = true,
            IsBestSeller = true
        },
        new ShoeItem
        {
            Id = 6,
            Name = "Adidas Forum Low Classic",
            Brand = "Adidas",
            Category = "Thời trang",
            Price = 2300000,
            OriginalPrice = 2600000,
            ImageUrl = "https://images.unsplash.com/photo-1518002171953-a080ee817e1f?w=800&auto=format&fit=crop&q=80",
            AvailableSizes = new() { 39, 40, 41, 42, 43 },
            Rating = 4.6,
            ReviewCount = 92,
            IsNew = false,
            IsBestSeller = false
        },
        new ShoeItem
        {
            Id = 7,
            Name = "Jordan Stay Loyal 3",
            Brand = "Jordan",
            Category = "Bóng rổ",
            Price = 3190000,
            OriginalPrice = null,
            ImageUrl = "https://images.unsplash.com/photo-1579338559194-a162d19bf842?w=800&auto=format&fit=crop&q=80",
            AvailableSizes = new() { 41, 42, 43, 44 },
            Rating = 4.8,
            ReviewCount = 63,
            IsNew = true,
            IsBestSeller = false
        },
        new ShoeItem
        {
            Id = 8,
            Name = "Puma Suede Classic XXI",
            Brand = "Puma",
            Category = "Đi hàng ngày",
            Price = 1890000,
            OriginalPrice = 2100000,
            ImageUrl = "https://images.unsplash.com/photo-1606107557195-0e29a4b5b4aa?w=800&auto=format&fit=crop&q=80",
            AvailableSizes = new() { 38, 39, 40, 41, 42 },
            Rating = 4.7,
            ReviewCount = 118,
            IsNew = false,
            IsBestSeller = true
        }
    };

    public static List<ShoeItem> GetAll() => _shoes;

    public static List<ShoeItem> GetByBrand(string brand)
    {
        if (string.IsNullOrWhiteSpace(brand) || brand.Equals("All", StringComparison.OrdinalIgnoreCase))
            return _shoes;
        return _shoes.Where(s => s.Brand.Equals(brand, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public static ShoeItem? GetById(int id) => _shoes.FirstOrDefault(s => s.Id == id);
}
