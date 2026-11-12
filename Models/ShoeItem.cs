namespace QuanLyBanGiay.Models;

public class ShoeItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty; // Nike, Adidas, Jordan, Puma
    public string Category { get; set; } = string.Empty; // Chạy bộ, Bóng rổ, Thời trang, Đi hàng ngày
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public List<int> AvailableSizes { get; set; } = new();
    public double Rating { get; set; } = 5.0;
    public int ReviewCount { get; set; } = 48;
    public bool IsNew { get; set; }
    public bool IsBestSeller { get; set; }
}
