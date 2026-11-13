using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyBanGiay.Data;
using QuanLyBanGiay.Models;
using QuanLyBanGiay.Models.Entities;

namespace QuanLyBanGiay.Controllers;

public class CartController(AppDbContext db, IDataProtectionProvider protectionProvider) : Controller
{
    private const string CartCookieName = "sneakerzone_cart";
    private const int MaximumQuantity = 99;
    private readonly IDataProtector _protector = protectionProvider.CreateProtector("QuanLyBanGiay.Storefront.Cart.v1");

    public async Task<IActionResult> Index()
    {
        ViewBag.CartError = TempData["CartError"] as string;
        return View(await BuildCartViewModelAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToCart(int shoeId, int size, int? variantId, string? color, int quantity = 1)
    {
        if (!await AddItemAsync(shoeId, size, variantId, color, quantity))
            TempData["CartError"] = "Sản phẩm, màu sắc, kích cỡ hoặc số lượng không hợp lệ / đã hết hàng.";
        return RedirectToAction("Index", "Home");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAjax(int shoeId, int size, int? variantId, string? color, int quantity = 1)
    {
        if (!await AddItemAsync(shoeId, size, variantId, color, quantity))
            return BadRequest(new { success = false, message = "Sản phẩm, màu sắc, kích cỡ hoặc số lượng không hợp lệ / đã hết hàng." });
        return Json(new { success = true, count = ReadCart().Items.Sum(x => x.Quantity), message = "Đã thêm vào giỏ hàng!" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> BuyNow(int shoeId, int size, int? variantId, string? color, int quantity = 1)
    {
        if (!await AddItemAsync(shoeId, size, variantId, color, quantity))
        {
            TempData["CartError"] = "Sản phẩm, màu sắc, kích cỡ hoặc số lượng không hợp lệ / đã hết hàng.";
            return RedirectToAction("Details", "Home", new { id = shoeId });
        }
        return RedirectToAction(nameof(Checkout));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Remove(int shoeId, int size)
    {
        var cart = ReadCart();
        cart.Items.RemoveAll(x => x.VariantId == shoeId && x.Size == size);
        WriteCart(cart);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateQuantity(int shoeId, int size, int quantity)
    {
        if (quantity is < 1 or > MaximumQuantity)
            TempData["CartError"] = "Số lượng cần nằm trong khoảng 1–99.";
        else
        {
            var cart = ReadCart();
            var item = cart.Items.FirstOrDefault(x => x.VariantId == shoeId && x.Size == size);
            var available = await db.ProductVariants.AsNoTracking().Where(v => v.Id == shoeId && v.Size == size)
                .Select(v => (int?)(v.StockQuantity - v.ReservedQuantity)).FirstOrDefaultAsync();
            if (item is null || available is null || quantity > available.Value)
                TempData["CartError"] = "Số lượng vượt quá tồn kho khả dụng.";
            else { item.Quantity = quantity; WriteCart(cart); }
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyVoucher(string? voucherCode)
    {
        var cart = ReadCart();
        var subtotal = await GetSubtotalAsync(cart);
        var voucher = await FindValidVoucherAsync(voucherCode, subtotal);
        if (voucher is null)
        {
            TempData["CartError"] = "Mã voucher không hợp lệ, chưa đến / đã hết hạn, hết lượt hoặc chưa đạt giá trị đơn tối thiểu.";
        }
        else
        {
            cart.VoucherCode = voucher.Code;
            WriteCart(cart);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Checkout()
    {
        var cart = ReadCart();
        if (cart.Items.Count == 0) return RedirectToAction(nameof(Index));
        return View(await BuildOrderViewModelAsync(cart));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(OrderViewModel order)
    {
        var cart = ReadCart();
        var checkout = await BuildOrderViewModelAsync(cart);
        if (checkout.Items.Count == 0) return RedirectToAction(nameof(Index));
        order.Items = checkout.Items;
        order.TotalAmount = checkout.TotalAmount;
        order.DiscountAmount = checkout.DiscountAmount;
        order.VoucherCode = checkout.VoucherCode;

        if (!ModelState.IsValid) return View(order);
        if (order.PaymentMethod is not ("COD" or "QR"))
        {
            ModelState.AddModelError(nameof(order.PaymentMethod), "Phương thức thanh toán không hợp lệ.");
            return View(order);
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var variants = await db.ProductVariants.Include(v => v.Product)
                .Where(v => cart.Items.Select(i => i.VariantId).Contains(v.Id)).ToDictionaryAsync(v => v.Id);
            if (variants.Count != cart.Items.Select(i => i.VariantId).Distinct().Count() ||
                cart.Items.Any(i => !variants.TryGetValue(i.VariantId, out var v) || !v.Product.IsActive || v.Size != i.Size))
                return await CheckoutStockFailure(transaction, order, "Một sản phẩm trong giỏ không còn được bán.");

            var subtotal = cart.Items.Sum(i => variants[i.VariantId].Product.RetailPrice * i.Quantity);
            var voucher = string.IsNullOrWhiteSpace(cart.VoucherCode) ? null : await FindValidVoucherAsync(cart.VoucherCode, subtotal);
            if (!string.IsNullOrWhiteSpace(cart.VoucherCode) && voucher is null)
            {
                cart.VoucherCode = "";
                WriteCart(cart);
                return await CheckoutStockFailure(transaction, order, "Voucher không còn đủ điều kiện áp dụng. Vui lòng kiểm tra giỏ hàng.");
            }
            var discount = voucher is null ? 0m : CalculateDiscount(voucher, subtotal);

            // Reserve each variant atomically so simultaneous checkouts cannot oversell.
            foreach (var item in cart.Items)
            {
                var affected = await db.ProductVariants.Where(v => v.Id == item.VariantId && v.Product.IsActive &&
                        v.Size == item.Size && v.StockQuantity - v.ReservedQuantity >= item.Quantity)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(v => v.ReservedQuantity, v => v.ReservedQuantity + item.Quantity));
                if (affected != 1)
                    return await CheckoutStockFailure(transaction, order, "Một sản phẩm vừa hết hàng hoặc số lượng khả dụng đã thay đổi. Vui lòng cập nhật giỏ hàng.");
            }

            if (voucher is not null)
            {
                var used = await db.Vouchers.Where(v => v.Id == voucher.Id && v.IsActive && v.UsedCount < v.UsageLimit &&
                        v.StartAt <= DateTime.UtcNow && v.EndAt >= DateTime.UtcNow)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(v => v.UsedCount, v => v.UsedCount + 1));
                if (used != 1) return await CheckoutStockFailure(transaction, order, "Voucher vừa hết lượt sử dụng.");
            }

            var paymentMethod = order.PaymentMethod == "QR" ? "QR_TRANSFER" : "COD";
            var entity = new Order
            {
                OrderCode = $"DH-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..24].ToUpperInvariant(),
                VoucherId = voucher?.Id,
                VoucherCode = voucher?.Code,
                CustomerName = order.CustomerName.Trim(), CustomerPhone = order.PhoneNumber.Trim(),
                ShippingAddress = order.Address.Trim(), Subtotal = subtotal, DiscountAmount = discount,
                ShippingFee = 0, TotalAmount = subtotal - discount, PaymentMethod = paymentMethod,
                PaymentStatus = "Pending", OrderStatus = "Pending", CreatedAt = DateTime.UtcNow
            };
            foreach (var item in cart.Items)
            {
                var v = variants[item.VariantId];
                entity.Items.Add(new OrderItem
                {
                    ProductVariantId = v.Id, ProductName = v.Product.Name, Size = v.Size, Color = v.Color,
                    UnitPrice = v.Product.RetailPrice, UnitCost = v.CostPrice, Quantity = item.Quantity,
                    TotalPrice = v.Product.RetailPrice * item.Quantity
                });
            }
            db.Orders.Add(entity);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();

            order.TotalAmount = entity.TotalAmount;
            order.DiscountAmount = entity.DiscountAmount;
            order.VoucherCode = entity.VoucherCode ?? "";
            order.PaymentMethod = entity.PaymentMethod;
            TempData["CompletedOrder"] = JsonSerializer.Serialize(order);
            ClearCart();
            return RedirectToAction(nameof(Success));
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            ModelState.AddModelError(string.Empty, "Không thể tạo đơn hàng do dữ liệu tồn kho vừa thay đổi. Vui lòng thử lại.");
            return View(await BuildOrderViewModelAsync(cart, order));
        }
    }

    [HttpGet]
    public IActionResult Success()
    {
        var serializedOrder = TempData["CompletedOrder"] as string;
        if (string.IsNullOrEmpty(serializedOrder)) return RedirectToAction(nameof(Index));
        var order = JsonSerializer.Deserialize<OrderViewModel>(serializedOrder);
        return order is null ? RedirectToAction(nameof(Index)) : View(order);
    }

    private async Task<bool> AddItemAsync(int productId, int size, int? variantId, string? color, int quantity)
    {
        if (quantity is < 1 or > MaximumQuantity) return false;
        var query = db.ProductVariants.Include(v => v.Product).Where(v => v.ProductId == productId && v.Size == size &&
            v.Product.IsActive && v.StockQuantity - v.ReservedQuantity >= quantity);
        if (variantId.HasValue) query = query.Where(v => v.Id == variantId.Value);
        if (!string.IsNullOrWhiteSpace(color)) query = query.Where(v => v.Color == color);
        var variant = await query.FirstOrDefaultAsync();
        if (variant is null) return false;
        var cart = ReadCart();
        var item = cart.Items.FirstOrDefault(i => i.VariantId == variant.Id);
        if (item is not null)
        {
            var combined = item.Quantity + quantity;
            if (combined > MaximumQuantity || combined > variant.StockQuantity - variant.ReservedQuantity) return false;
            item.Quantity = combined;
        }
        else cart.Items.Add(new CartCookieItem { VariantId = variant.Id, Size = variant.Size, Quantity = quantity });
        WriteCart(cart);
        return true;
    }

    private async Task<CartViewModel> BuildCartViewModelAsync()
    {
        var cart = ReadCart();
        var variants = await LoadCartVariantsAsync(cart);
        var items = cart.Items.Where(i => variants.ContainsKey(i.VariantId)).Select(i =>
        {
            var v = variants[i.VariantId];
            return new CartItem { ShoeId = v.Id, ShoeName = v.Product.Name + " — " + v.Color, Brand = v.Product.Brand,
                ImageUrl = v.ImageUrl ?? v.Product.ImageUrl, Price = v.Product.RetailPrice, Size = v.Size, Quantity = i.Quantity };
        }).ToList();
        var subtotal = items.Sum(i => i.Price * i.Quantity);
        var voucher = string.IsNullOrWhiteSpace(cart.VoucherCode) ? null : await FindValidVoucherAsync(cart.VoucherCode, subtotal);
        return new CartViewModel { Items = items, Subtotal = subtotal, DiscountAmount = voucher is null ? 0 : CalculateDiscount(voucher, subtotal),
            TotalAmount = subtotal - (voucher is null ? 0 : CalculateDiscount(voucher, subtotal)), VoucherCode = voucher?.Code ?? "" };
    }

    private async Task<OrderViewModel> BuildOrderViewModelAsync(CartCookieState cart, OrderViewModel? form = null)
    {
        var vm = await BuildCartViewModelAsync();
        return new OrderViewModel { CustomerName = form?.CustomerName ?? "", PhoneNumber = form?.PhoneNumber ?? "",
            Address = form?.Address ?? "", Note = form?.Note ?? "", PaymentMethod = form?.PaymentMethod ?? "COD",
            Items = vm.Items.ToList(), TotalAmount = vm.TotalAmount,
            DiscountAmount = vm.DiscountAmount, VoucherCode = vm.VoucherCode };
    }

    private async Task<Dictionary<int, ProductVariant>> LoadCartVariantsAsync(CartCookieState cart)
    {
        var ids = cart.Items.Select(i => i.VariantId).Distinct().ToList();
        if (ids.Count == 0) return new();
        return await db.ProductVariants.AsNoTracking().Include(v => v.Product).Where(v => ids.Contains(v.Id)).ToDictionaryAsync(v => v.Id);
    }

    private async Task<decimal> GetSubtotalAsync(CartCookieState cart)
    {
        var variants = await LoadCartVariantsAsync(cart);
        return cart.Items.Where(i => variants.ContainsKey(i.VariantId)).Sum(i => variants[i.VariantId].Product.RetailPrice * i.Quantity);
    }

    private async Task<Voucher?> FindValidVoucherAsync(string? code, decimal subtotal)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var now = DateTime.UtcNow;
        return await db.Vouchers.AsNoTracking().FirstOrDefaultAsync(v => v.Code == code.Trim() && v.IsActive &&
            v.StartAt <= now && v.EndAt >= now && v.UsedCount < v.UsageLimit && subtotal >= v.MinOrderAmount);
    }

    private static decimal CalculateDiscount(Voucher voucher, decimal subtotal)
    {
        var discount = voucher.DiscountType.Equals("PERCENT", StringComparison.OrdinalIgnoreCase) ||
                       voucher.DiscountType.Equals("PERCENTAGE", StringComparison.OrdinalIgnoreCase)
            ? decimal.Round(subtotal * voucher.DiscountValue / 100m, 0)
            : voucher.DiscountType.Equals("FIXED_AMOUNT", StringComparison.OrdinalIgnoreCase) ||
              voucher.DiscountType.Equals("FIXED", StringComparison.OrdinalIgnoreCase)
                ? voucher.DiscountValue : 0m;
        if (voucher.MaxDiscountAmount.HasValue) discount = Math.Min(discount, voucher.MaxDiscountAmount.Value);
        return Math.Clamp(discount, 0, subtotal);
    }

    private CartCookieState ReadCart()
    {
        if (!Request.Cookies.TryGetValue(CartCookieName, out var value)) return new();
        try { return JsonSerializer.Deserialize<CartCookieState>(_protector.Unprotect(value)) ?? new(); }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or JsonException) { return new(); }
    }

    private void WriteCart(CartCookieState cart)
    {
        var payload = JsonSerializer.Serialize(cart);
        Response.Cookies.Append(CartCookieName, _protector.Protect(payload), new CookieOptions
        { HttpOnly = true, IsEssential = true, SameSite = SameSiteMode.Lax, Secure = Request.IsHttps, MaxAge = TimeSpan.FromDays(30) });
    }

    private void ClearCart() => Response.Cookies.Delete(CartCookieName);

    private async Task<IActionResult> CheckoutStockFailure(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction, OrderViewModel order, string message)
    {
        await transaction.RollbackAsync();
        ModelState.AddModelError(string.Empty, message);
        return View(await BuildOrderViewModelAsync(ReadCart(), order));
    }

    private sealed class CartCookieState
    {
        public CartCookieState() { }
        public List<CartCookieItem> Items { get; set; } = new();
        public string VoucherCode { get; set; } = "";
    }

    private sealed class CartCookieItem
    {
        public CartCookieItem() { }
        public int VariantId { get; set; }
        public int Size { get; set; }
        public int Quantity { get; set; }
    }
}
