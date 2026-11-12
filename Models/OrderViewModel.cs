namespace QuanLyBanGiay.Models;

public class OrderViewModel
{
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    public string CustomerName { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [System.ComponentModel.DataAnnotations.Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
    public string PhoneNumber { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Vui lòng nhập địa chỉ nhận hàng.")]
    public string Address { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Vui lòng chọn phương thức thanh toán.")]
    [System.ComponentModel.DataAnnotations.RegularExpression("^(COD|QR|CARD)$", ErrorMessage = "Phương thức thanh toán không hợp lệ.")]
    public string PaymentMethod { get; set; } = "COD";

    public string Note { get; set; } = string.Empty;
    public List<CartItem> Items { get; set; } = new();
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string VoucherCode { get; set; } = string.Empty;
}
