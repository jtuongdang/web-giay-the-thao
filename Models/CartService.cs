using System.Collections.Concurrent;

namespace QuanLyBanGiay.Models;

public static class CartService
{
    public const string DiscountVoucher = "SNEAKER10";
    private const int MaximumQuantity = 99;
    private static readonly ConcurrentDictionary<string, CartState> Carts = new(StringComparer.Ordinal);

    public static IReadOnlyList<CartItem> GetItems(string cartId)
    {
        var cart = GetCart(cartId);
        lock (cart)
        {
            return cart.Items.Select(Copy).ToList();
        }
    }

    public static int GetTotalCount(string? cartId)
    {
        if (string.IsNullOrEmpty(cartId) || !Carts.TryGetValue(cartId, out var cart))
            return 0;
        lock (cart)
        {
            return cart.Items.Sum(x => x.Quantity);
        }
    }

    public static string GetVoucherCode(string cartId)
    {
        var cart = GetCart(cartId);
        lock (cart)
        {
            return cart.VoucherCode;
        }
    }

    public static void Add(string cartId, CartItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Quantity is < 1 or > MaximumQuantity)
            throw new ArgumentOutOfRangeException(nameof(item), "Quantity must be between 1 and 99.");

        var cart = GetCart(cartId);
        lock (cart)
        {
            var existing = cart.Items.FirstOrDefault(i => i.ShoeId == item.ShoeId && i.Size == item.Size);
            if (existing is null)
            {
                cart.Items.Add(Copy(item));
                return;
            }

            existing.Quantity = Math.Min(MaximumQuantity, existing.Quantity + item.Quantity);
        }
    }

    public static bool UpdateQuantity(string cartId, int shoeId, int size, int quantity)
    {
        if (quantity is < 1 or > MaximumQuantity)
            return false;

        var cart = GetCart(cartId);
        lock (cart)
        {
            var item = cart.Items.FirstOrDefault(i => i.ShoeId == shoeId && i.Size == size);
            if (item is null)
                return false;

            item.Quantity = quantity;
            return true;
        }
    }

    public static bool Remove(string cartId, int shoeId, int size)
    {
        var cart = GetCart(cartId);
        lock (cart)
        {
            var item = cart.Items.FirstOrDefault(i => i.ShoeId == shoeId && i.Size == size);
            return item is not null && cart.Items.Remove(item);
        }
    }

    public static bool ApplyVoucher(string cartId, string? voucherCode)
    {
        if (!string.Equals(voucherCode?.Trim(), DiscountVoucher, StringComparison.OrdinalIgnoreCase))
            return false;

        var cart = GetCart(cartId);
        lock (cart)
        {
            cart.VoucherCode = DiscountVoucher;
        }

        return true;
    }

    public static void Clear(string cartId)
    {
        var cart = GetCart(cartId);
        lock (cart)
        {
            cart.Items.Clear();
            cart.VoucherCode = string.Empty;
        }
    }

    private static CartState GetCart(string cartId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cartId);
        return Carts.GetOrAdd(cartId, _ => new CartState());
    }

    private static CartItem Copy(CartItem item) => new()
    {
        ShoeId = item.ShoeId,
        ShoeName = item.ShoeName,
        Brand = item.Brand,
        ImageUrl = item.ImageUrl,
        Price = item.Price,
        Size = item.Size,
        Quantity = item.Quantity
    };

    private sealed class CartState
    {
        public List<CartItem> Items { get; } = new();
        public string VoucherCode { get; set; } = string.Empty;
    }
}
