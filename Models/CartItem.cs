namespace QuanLyBanGiay.Models;

public class CartItem
{
    public int ShoeId { get; set; }
    public string ShoeName { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Size { get; set; }
    public int Quantity { get; set; } = 1;
}

public class CartViewModel
{
    public IReadOnlyList<CartItem> Items { get; set; } = Array.Empty<CartItem>();
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string VoucherCode { get; set; } = string.Empty;
}
