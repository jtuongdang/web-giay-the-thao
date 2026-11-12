using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using QuanLyBanGiay.Models;

namespace QuanLyBanGiay.Controllers;

public class CartController : Controller
{
    private const string CartCookieName = "sneakerzone_cart";

    public IActionResult Index()
    {
        ViewBag.CartError = TempData["CartError"] as string;
        return View(BuildCartViewModel(GetCartId()));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddToCart(int shoeId, int size, int quantity = 1)
    {
        var shoe = ShoeDataService.GetById(shoeId);
        if (shoe is null || !shoe.AvailableSizes.Contains(size) || quantity is < 1 or > 99)
        {
            TempData["CartError"] = "Sản phẩm, kích cỡ hoặc số lượng không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        CartService.Add(GetCartId(), new CartItem
        {
            ShoeId = shoe.Id,
            ShoeName = shoe.Name,
            Brand = shoe.Brand,
            ImageUrl = shoe.ImageUrl,
            Price = shoe.Price,
            Size = size,
            Quantity = quantity
        });

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddAjax(int shoeId, int size, int quantity = 1)
    {
        var shoe = ShoeDataService.GetById(shoeId);
        if (shoe is null || !shoe.AvailableSizes.Contains(size) || quantity is < 1 or > 99)
        {
            return BadRequest(new
            {
                success = false,
                message = "Sản phẩm, kích cỡ hoặc số lượng không hợp lệ."
            });
        }

        var cartId = GetCartId();
        CartService.Add(cartId, new CartItem
        {
            ShoeId = shoe.Id,
            ShoeName = shoe.Name,
            Brand = shoe.Brand,
            ImageUrl = shoe.ImageUrl,
            Price = shoe.Price,
            Size = size,
            Quantity = quantity
        });

        return Json(new
        {
            success = true,
            count = CartService.GetTotalCount(cartId),
            message = "Đã thêm vào giỏ hàng!"
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Remove(int shoeId, int size)
    {
        CartService.Remove(GetCartId(), shoeId, size);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateQuantity(int shoeId, int size, int quantity)
    {
        if (!CartService.UpdateQuantity(GetCartId(), shoeId, size, quantity))
            TempData["CartError"] = "Số lượng cần nằm trong khoảng 1–99.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ApplyVoucher(string? voucherCode)
    {
        if (!CartService.ApplyVoucher(GetCartId(), voucherCode))
            TempData["CartError"] = $"Mã không hợp lệ. Hãy thử {CartService.DiscountVoucher}.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Checkout()
    {
        var cartId = GetCartId();
        if (CartService.GetItems(cartId).Count == 0)
            return RedirectToAction(nameof(Index));

        return View(BuildOrderViewModel(cartId));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Checkout(OrderViewModel order)
    {
        var cartId = GetCartId();
        var checkout = BuildOrderViewModel(cartId);
        if (checkout.Items.Count == 0)
            return RedirectToAction(nameof(Index));

        order.Items = checkout.Items;
        order.TotalAmount = checkout.TotalAmount;
        order.DiscountAmount = checkout.DiscountAmount;
        order.VoucherCode = checkout.VoucherCode;

        if (!ModelState.IsValid)
            return View(order);

        TempData["CompletedOrder"] = JsonSerializer.Serialize(order);
        CartService.Clear(cartId);
        return RedirectToAction(nameof(Success));
    }

    [HttpGet]
    public IActionResult Success()
    {
        var serializedOrder = TempData["CompletedOrder"] as string;
        if (string.IsNullOrEmpty(serializedOrder))
            return RedirectToAction(nameof(Index));

        var order = JsonSerializer.Deserialize<OrderViewModel>(serializedOrder);
        return order is null ? RedirectToAction(nameof(Index)) : View(order);
    }

    private string GetCartId()
    {
        if (Request.Cookies.TryGetValue(CartCookieName, out var existingId) &&
            Guid.TryParseExact(existingId, "N", out _))
        {
            return existingId;
        }

        var cartId = Guid.NewGuid().ToString("N");
        Response.Cookies.Append(CartCookieName, cartId, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            MaxAge = TimeSpan.FromDays(30)
        });
        return cartId;
    }

    private static CartViewModel BuildCartViewModel(string cartId)
    {
        var items = CartService.GetItems(cartId);
        var subtotal = items.Sum(item => item.Price * item.Quantity);
        var voucherCode = CartService.GetVoucherCode(cartId);
        var discount = string.Equals(voucherCode, CartService.DiscountVoucher, StringComparison.OrdinalIgnoreCase)
            ? decimal.Round(subtotal * 0.10m, 0)
            : 0m;

        return new CartViewModel
        {
            Items = items,
            Subtotal = subtotal,
            DiscountAmount = discount,
            TotalAmount = subtotal - discount,
            VoucherCode = voucherCode
        };
    }

    private static OrderViewModel BuildOrderViewModel(string cartId)
    {
        var cart = BuildCartViewModel(cartId);
        return new OrderViewModel
        {
            Items = cart.Items.ToList(),
            TotalAmount = cart.TotalAmount,
            DiscountAmount = cart.DiscountAmount,
            VoucherCode = cart.VoucherCode
        };
    }
}
