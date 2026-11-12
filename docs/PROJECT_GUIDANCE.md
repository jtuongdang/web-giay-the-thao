# PROJECT GUIDANCE & ARCHITECTURE: QUAN LY BAN GIAY (SHOE STORE)

## 1. TỔNG QUAN HỆ THỐNG
Dự án **Quản lý bán giày** xây dựng trên nền tảng **ASP.NET Core 10 (MVC)**.
Mục tiêu là xây dựng giao diện bán hàng thương mại điện tử hiện đại, đẳng cấp lấy cảm hứng từ Nike và Adidas:
- Tông màu: Sleek Monochromatic (#111111 đen sâu, #F5F5F5 xám nhạt, viền tinh tế, điểm nhấn cam/đỏ thể thao).
- Typography: Phông chữ sans-serif khỏe khoắn, kích thước rõ ràng, phân cấp thị giác mạnh.
- Trải nghiệm người dùng: Lưới sản phẩm dạng thẻ chuẩn `<article>` tự co giãn theo Container Queries, thanh điều hướng ghim cố định (sticky header), bộ lọc đa tiêu chí (hãng, kích cỡ, mức giá), giỏ hàng phản hồi nhanh.

## 2. QUY CHUẨN DESIGN SYSTEM & CSS (THEO CHUẨN HIỆN ĐẠI)
- Lưới sản phẩm: Dùng `display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 1.5rem;`
- Thẻ sản phẩm (`.shoe-card`):
  - Dùng thẻ ngữ nghĩa `<article class="shoe-card">`
  - Ảnh giày: `aspect-ratio: 4/3; object-fit: cover;` có hiệu ứng chuyển động mượt mà khi hover (`transform: scale(1.05); transition: transform 0.3s ease;`)
  - Tag thương hiệu và giá tiền nổi bật, có nút "Thêm vào giỏ" (Quick Add).
- Header:
  - Sticky navbar với logo phong cách thể thao.
  - Menu phân loại: "Tất cả", "Nike", "Adidas", "Jordan", "Puma".
  - Ô tìm kiếm nhanh và biểu tượng giỏ hàng có huy hiệu (badge) đếm số lượng.

## 3. CẤU TRÚC DỮ LIỆU CHUẨN (MODELS)
```csharp
namespace QuanLyBanGiay.Models;

public class ShoeItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty; // Nike, Adidas, Jordan, Puma
    public string Category { get; set; } = string.Empty; // Chạy bộ, Bóng rổ, Thời trang
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; } // Giá gốc (nếu có giảm giá)
    public string ImageUrl { get; set; } = string.Empty;
    public List<int> AvailableSizes { get; set; } = new();
    public double Rating { get; set; } = 5.0;
    public bool IsNew { get; set; }
    public bool IsBestSeller { get; set; }
}
```
