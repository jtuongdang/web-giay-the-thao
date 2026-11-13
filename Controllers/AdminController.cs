using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyBanGiay.Data;
using QuanLyBanGiay.Models.Entities;

namespace QuanLyBanGiay.Controllers;

public class AdminController(AppDbContext db, ILogger<AdminController> logger) : Controller
{
    private static readonly CultureInfo VnCulture = new("vi-VN");

    // ==========================================
    // 1. DASHBOARD
    // ==========================================
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["ActiveMenu"] = "Dashboard";

        // Completed orders (revenue recognizing)
        var completedOrders = await db.Orders
            .Include(o => o.Items)
            .Where(o => o.OrderStatus == "Completed" || o.PaymentStatus == "Paid")
            .ToListAsync();

        // 1. Net Revenue: TotalAmount of completed orders minus customer refunds
        var totalOrderRevenue = completedOrders.Sum(o => o.TotalAmount);
        var totalCustomerRefunds = await db.CashFlowLedger
            .Where(c => c.VoucherType == "PAYMENT" && c.Category == "CUSTOMER_REFUND")
            .SumAsync(c => (decimal?)c.Amount) ?? 0m;
        var netRevenue = Math.Max(0, totalOrderRevenue - totalCustomerRefunds);

        // 2. COGS (Cost of Goods Sold): sum(UnitCost * Quantity) of completed order items
        var cogs = completedOrders
            .SelectMany(o => o.Items)
            .Sum(i => i.UnitCost * i.Quantity);

        // 3. Gross Profit = Net Revenue - COGS
        var grossProfit = netRevenue - cogs;

        // 4. Cash Flow Balance (Sổ quỹ thực tế): Receipts - Payments
        var totalCashInflow = await db.CashFlowLedger
            .Where(c => c.VoucherType == "RECEIPT")
            .SumAsync(c => (decimal?)c.Amount) ?? 0m;
        var totalCashOutflow = await db.CashFlowLedger
            .Where(c => c.VoucherType == "PAYMENT")
            .SumAsync(c => (decimal?)c.Amount) ?? 0m;
        var netCashBalance = totalCashInflow - totalCashOutflow;

        // Additional KPI metrics
        var pendingOrdersCount = await db.Orders.CountAsync(o => o.OrderStatus == "Pending");
        var totalSupplierDebt = await db.Suppliers.SumAsync(s => (decimal?)s.CurrentDebt) ?? 0m;
        
        // Low stock count (StockQuantity <= MinStockAlert)
        var lowStockVariantsCount = await db.ProductVariants
            .CountAsync(v => v.StockQuantity <= v.MinStockAlert);

        // Low stock variants for alert section (Top 8 urgently low)
        var lowStockAlerts = await db.ProductVariants
            .Include(v => v.Product)
            .Where(v => v.StockQuantity <= v.MinStockAlert)
            .OrderBy(v => v.StockQuantity)
            .Take(8)
            .Select(v => new LowStockItemViewModel
            {
                VariantId = v.Id,
                ProductName = v.Product.Name,
                Brand = v.Product.Brand,
                Size = v.Size,
                Color = v.Color,
                Sku = v.VariantSku,
                StockQuantity = v.StockQuantity,
                ReservedQuantity = v.ReservedQuantity,
                AvailableQuantity = v.StockQuantity - v.ReservedQuantity,
                MinStockAlert = v.MinStockAlert,
                CostPrice = v.CostPrice,
                RetailPrice = v.Product.RetailPrice
            })
            .ToListAsync();

        // Recent orders (last 6)
        var recentOrders = await db.Orders
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .Take(6)
            .ToListAsync();

        var vm = new AdminDashboardViewModel
        {
            NetRevenue = netRevenue,
            Cogs = cogs,
            GrossProfit = grossProfit,
            NetCashBalance = netCashBalance,
            TotalCashInflow = totalCashInflow,
            TotalCashOutflow = totalCashOutflow,
            PendingOrdersCount = pendingOrdersCount,
            TotalSupplierDebt = totalSupplierDebt,
            LowStockVariantsCount = lowStockVariantsCount,
            LowStockAlerts = lowStockAlerts,
            RecentOrders = recentOrders
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> GetChartData()
    {
        // Daily aggregated revenue, gross profit, cash flow for the last 14 days
        var startDate = DateTime.UtcNow.Date.AddDays(-13);

        var days = Enumerable.Range(0, 14)
            .Select(i => startDate.AddDays(i))
            .ToList();

        var orders = await db.Orders
            .Include(o => o.Items)
            .Where(o => o.CreatedAt >= startDate && (o.OrderStatus == "Completed" || o.PaymentStatus == "Paid"))
            .ToListAsync();

        var cashFlowEntries = await db.CashFlowLedger
            .Where(c => c.CreatedAt >= startDate)
            .ToListAsync();

        var labels = days.Select(d => d.ToString("dd/MM")).ToList();
        var revenueData = new List<decimal>();
        var profitData = new List<decimal>();
        var cashInflowData = new List<decimal>();
        var cashOutflowData = new List<decimal>();

        foreach (var day in days)
        {
            var nextDay = day.AddDays(1);
            var dayOrders = orders.Where(o => o.CreatedAt >= day && o.CreatedAt < nextDay).ToList();
            var dayRevenue = dayOrders.Sum(o => o.TotalAmount);
            var dayCogs = dayOrders.SelectMany(o => o.Items).Sum(i => i.UnitCost * i.Quantity);
            var dayProfit = dayRevenue - dayCogs;

            var dayReceipts = cashFlowEntries
                .Where(c => c.CreatedAt >= day && c.CreatedAt < nextDay && c.VoucherType == "RECEIPT")
                .Sum(c => c.Amount);
            var dayPayments = cashFlowEntries
                .Where(c => c.CreatedAt >= day && c.CreatedAt < nextDay && c.VoucherType == "PAYMENT")
                .Sum(c => c.Amount);

            revenueData.Add(dayRevenue);
            profitData.Add(dayProfit);
            cashInflowData.Add(dayReceipts);
            cashOutflowData.Add(dayPayments);
        }

        return Json(new
        {
            labels,
            revenue = revenueData,
            profit = profitData,
            cashInflow = cashInflowData,
            cashOutflow = cashOutflowData
        });
    }

    // ==========================================
    // 2. ORDERS MANAGEMENT & STATE MACHINE
    // ==========================================
    [HttpGet]
    public async Task<IActionResult> Orders(string? status, string? search, int page = 1)
    {
        ViewData["ActiveMenu"] = "Orders";
        const int pageSize = 15;

        var query = db.Orders
            .Include(o => o.Items)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(o => o.OrderStatus == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim().ToLower();
            query = query.Where(o => o.OrderCode.ToLower().Contains(kw) ||
                                     o.CustomerName.ToLower().Contains(kw) ||
                                     o.CustomerPhone.ToLower().Contains(kw));
        }

        var totalItems = await query.CountAsync();
        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.CurrentStatus = status ?? "All";
        ViewBag.SearchQuery = search ?? "";
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        ViewBag.TotalCount = totalItems;

        // Pending count for badge
        ViewBag.PendingCount = await db.Orders.CountAsync(o => o.OrderStatus == "Pending");

        return View(orders);
    }

    [HttpGet]
    public async Task<IActionResult> OrderDetail(int id)
    {
        var order = await db.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.ProductVariant)
            .Include(o => o.Returns)
                .ThenInclude(r => r.Items)
            .Include(o => o.CashFlowEntries)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy đơn hàng.";
            return RedirectToAction(nameof(Orders));
        }

        return View(order);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateOrderStatus(int orderId, string newStatus, string? returnUrl)
    {
        var order = await db.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.ProductVariant)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy đơn hàng.";
            return RedirectToLocal(returnUrl);
        }

        var currentStatus = order.OrderStatus;

        // Validate state machine transitions
        // Valid transitions:
        // Pending -> Confirmed
        // Pending -> Cancelled
        // Confirmed -> Shipping
        // Confirmed -> Cancelled
        // Shipping -> Completed
        // Shipping -> Cancelled
        // Completed/Cancelled -> Cannot transition to any other status
        if (currentStatus is "Completed" or "Cancelled")
        {
            TempData["ErrorMessage"] = $"Đơn hàng đã ở trạng thái {currentStatus}, không thể thay đổi trạng thái nữa.";
            return RedirectToLocal(returnUrl);
        }

        var isValidTransition = (currentStatus, newStatus) switch
        {
            ("Pending", "Confirmed") => true,
            ("Pending", "Cancelled") => true,
            ("Confirmed", "Shipping") => true,
            ("Confirmed", "Cancelled") => true,
            ("Shipping", "Completed") => true,
            ("Shipping", "Cancelled") => true,
            _ => false
        };

        if (!isValidTransition)
        {
            TempData["ErrorMessage"] = $"Chuyển trạng thái từ '{currentStatus}' sang '{newStatus}' không hợp lệ theo quy trình vận hành.";
            return RedirectToLocal(returnUrl);
        }

        var now = DateTime.UtcNow;

        if (newStatus == "Confirmed")
        {
            // Transition: Pending -> Confirmed
            // Convert reservation to actual stock reduction
            foreach (var item in order.Items)
            {
                var variant = item.ProductVariant;
                var oldStock = variant.StockQuantity;
                variant.StockQuantity = Math.Max(0, variant.StockQuantity - item.Quantity);
                variant.ReservedQuantity = Math.Max(0, variant.ReservedQuantity - item.Quantity);
                variant.UpdatedAt = now;

                db.InventoryTransactions.Add(new InventoryTransaction
                {
                    TxCode = $"EXP-{order.Id}-{variant.Id}-{now:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}",
                    ProductVariantId = variant.Id,
                    TxType = "EXPORT_SALE",
                    QuantityChange = -item.Quantity,
                    QuantityBefore = oldStock,
                    QuantityAfter = variant.StockQuantity,
                    ReferenceType = "Order",
                    ReferenceId = order.Id,
                    CreatedAt = now,
                    Note = $"Xuất kho bán hàng cho đơn {order.OrderCode}"
                });
            }
            order.OrderStatus = "Confirmed";
            TempData["SuccessMessage"] = $"Đã duyệt đơn {order.OrderCode}. Kho đã được cập nhật xuất hàng.";
        }
        else if (newStatus == "Shipping")
        {
            // Transition: Confirmed -> Shipping
            order.OrderStatus = "Shipping";
            TempData["SuccessMessage"] = $"Đơn hàng {order.OrderCode} đã chuyển sang trạng thái đang giao hàng.";
        }
        else if (newStatus == "Completed")
        {
            // Transition: Shipping -> Completed
            order.OrderStatus = "Completed";

            // If COD, mark as Paid and record cash receipt
            if (order.PaymentMethod == "COD" && order.PaymentStatus != "Paid")
            {
                order.PaymentStatus = "Paid";
                db.CashFlowLedger.Add(new CashFlowLedger
                {
                    VoucherCode = $"PT-{order.Id}-{now:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}",
                    VoucherType = "RECEIPT",
                    Category = "SALE_REVENUE",
                    Amount = order.TotalAmount,
                    PaymentMethod = "COD",
                    ReferenceType = "Order",
                    ReferenceId = order.Id,
                    OrderId = order.Id,
                    CreatedAt = now,
                    Note = $"Thu tiền đơn hàng COD {order.OrderCode}"
                });
            }
            TempData["SuccessMessage"] = $"Đơn hàng {order.OrderCode} đã giao thành công và hoàn tất.";
        }
        else if (newStatus == "Cancelled")
        {
            if (currentStatus == "Pending")
            {
                // Pending -> Cancelled: Just release reserved stock
                foreach (var item in order.Items)
                {
                    var variant = item.ProductVariant;
                    variant.ReservedQuantity = Math.Max(0, variant.ReservedQuantity - item.Quantity);
                    variant.UpdatedAt = now;
                }
            }
            else if (currentStatus is "Confirmed" or "Shipping")
            {
                // Confirmed/Shipping -> Cancelled: Restock actual inventory
                foreach (var item in order.Items)
                {
                    var variant = item.ProductVariant;
                    var oldStock = variant.StockQuantity;
                    variant.StockQuantity += item.Quantity;
                    variant.UpdatedAt = now;

                    db.InventoryTransactions.Add(new InventoryTransaction
                    {
                        TxCode = $"RET-{order.Id}-{variant.Id}-{now:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}",
                        ProductVariantId = variant.Id,
                        TxType = "RETURN",
                        QuantityChange = item.Quantity,
                        QuantityBefore = oldStock,
                        QuantityAfter = variant.StockQuantity,
                        ReferenceType = "Order",
                        ReferenceId = order.Id,
                        CreatedAt = now,
                        Note = $"Hoàn trả kho do hủy đơn {order.OrderCode}"
                    });
                }

                // If already Paid, refund customer
                if (order.PaymentStatus == "Paid")
                {
                    order.PaymentStatus = "Refunded";
                    db.CashFlowLedger.Add(new CashFlowLedger
                    {
                        VoucherCode = $"PC-REF-{order.Id}-{now:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}",
                        VoucherType = "PAYMENT",
                        Category = "CUSTOMER_REFUND",
                        Amount = order.TotalAmount,
                        PaymentMethod = order.PaymentMethod,
                        ReferenceType = "Order",
                        ReferenceId = order.Id,
                        OrderId = order.Id,
                        CreatedAt = now,
                        Note = $"Hoàn tiền cho khách do hủy đơn {order.OrderCode}"
                    });
                }
            }
            order.OrderStatus = "Cancelled";
            TempData["SuccessMessage"] = $"Đã hủy đơn hàng {order.OrderCode} thành công.";
        }

        await db.SaveChangesAsync();
        return RedirectToLocal(returnUrl);
    }

    // ==========================================
    // 3. RETURNS & EXCHANGES (RMA)
    // ==========================================
    [HttpGet]
    public async Task<IActionResult> Returns(string? search, int page = 1)
    {
        ViewData["ActiveMenu"] = "Returns";
        const int pageSize = 15;

        var query = db.OrderReturns
            .Include(r => r.Order)
            .Include(r => r.Items)
                .ThenInclude(i => i.OriginalVariant)
                    .ThenInclude(v => v.Product)
            .Include(r => r.Items)
                .ThenInclude(i => i.ExchangeVariant)
                    .ThenInclude(v => v.Product)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim().ToLower();
            query = query.Where(r => r.ReturnCode.ToLower().Contains(kw) ||
                                     r.Order.OrderCode.ToLower().Contains(kw) ||
                                     r.Reason.ToLower().Contains(kw));
        }

        var totalItems = await query.CountAsync();
        var returns = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Get completed orders for creating RMA
        ViewBag.CompletedOrders = await db.Orders
            .Where(o => o.OrderStatus == "Completed")
            .OrderByDescending(o => o.CreatedAt)
            .Take(50)
            .Select(o => new { o.Id, o.OrderCode, o.CustomerName, o.TotalAmount })
            .ToListAsync();

        // Available variants for exchange
        ViewBag.AvailableVariants = await db.ProductVariants
            .Include(v => v.Product)
            .Where(v => v.StockQuantity - v.ReservedQuantity > 0)
            .OrderBy(v => v.Product.Name)
            .ThenBy(v => v.Size)
            .Select(v => new
            {
                v.Id,
                v.VariantSku,
                v.Product.Name,
                v.Product.Brand,
                v.Size,
                v.Color,
                Available = v.StockQuantity - v.ReservedQuantity,
                Price = v.Product.RetailPrice
            })
            .ToListAsync();

        ViewBag.SearchQuery = search ?? "";
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        ViewBag.TotalCount = totalItems;

        return View(returns);
    }

    [HttpGet]
    public async Task<IActionResult> GetOrderItemsForReturn(int orderId)
    {
        var items = await db.OrderItems
            .Include(i => i.ProductVariant)
            .Where(i => i.OrderId == orderId)
            .Select(i => new
            {
                i.Id,
                i.ProductVariantId,
                i.ProductName,
                i.Size,
                i.Color,
                i.UnitPrice,
                i.Quantity
            })
            .ToListAsync();

        return Json(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateReturn(CreateReturnInputModel model)
    {
        if (model.OrderId <= 0 || model.OriginalVariantId <= 0 || model.Quantity <= 0)
        {
            TempData["ErrorMessage"] = "Thông tin yêu cầu đổi trả không hợp lệ.";
            return RedirectToAction(nameof(Returns));
        }

        var order = await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == model.OrderId);

        if (order == null || order.OrderStatus != "Completed")
        {
            TempData["ErrorMessage"] = "Chỉ được tạo yêu cầu đổi trả cho đơn hàng đã hoàn tất (Completed).";
            return RedirectToAction(nameof(Returns));
        }

        var originalItem = order.Items.FirstOrDefault(i => i.ProductVariantId == model.OriginalVariantId);
        if (originalItem == null || originalItem.Quantity < model.Quantity)
        {
            TempData["ErrorMessage"] = "Sản phẩm hoặc số lượng đổi trả vượt quá số lượng trong đơn hàng.";
            return RedirectToAction(nameof(Returns));
        }

        var originalVariant = await db.ProductVariants.FindAsync(model.OriginalVariantId);
        if (originalVariant == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy biến thể sản phẩm gốc.";
            return RedirectToAction(nameof(Returns));
        }

        var now = DateTime.UtcNow;
        var returnCode = $"RMA-{now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";

        if (model.ReturnType == "EXCHANGE")
        {
            if (!model.ExchangeVariantId.HasValue || model.ExchangeVariantId.Value <= 0)
            {
                TempData["ErrorMessage"] = "Vui lòng chọn biến thể muốn đổi sang.";
                return RedirectToAction(nameof(Returns));
            }

            var exchangeVariant = await db.ProductVariants
                .Include(v => v.Product)
                .FirstOrDefaultAsync(v => v.Id == model.ExchangeVariantId.Value);

            if (exchangeVariant == null)
            {
                TempData["ErrorMessage"] = "Biến thể đổi sang không tồn tại.";
                return RedirectToAction(nameof(Returns));
            }

            var availableStock = exchangeVariant.StockQuantity - exchangeVariant.ReservedQuantity;
            if (availableStock < model.Quantity)
            {
                TempData["ErrorMessage"] = $"Biến thể đổi sang không đủ hàng trong kho (Khả dụng: {availableStock}, yêu cầu: {model.Quantity}).";
                return RedirectToAction(nameof(Returns));
            }

            // 1. Restock original variant if resellable
            if (model.IsResellable)
            {
                var oldStock = originalVariant.StockQuantity;
                originalVariant.StockQuantity += model.Quantity;
                originalVariant.UpdatedAt = now;

                db.InventoryTransactions.Add(new InventoryTransaction
                {
                    TxCode = $"RMA-IN-{returnCode}-{Random.Shared.Next(100, 999)}",
                    ProductVariantId = originalVariant.Id,
                    TxType = "RETURN",
                    QuantityChange = model.Quantity,
                    QuantityBefore = oldStock,
                    QuantityAfter = originalVariant.StockQuantity,
                    ReferenceType = "OrderReturn",
                    ReferenceId = order.Id,
                    CreatedAt = now,
                    Note = $"Đổi hàng: Nhập lại hàng cũ (RMA {returnCode})"
                });
            }

            // 2. Reduce stock for exchange variant
            var oldExchangeStock = exchangeVariant.StockQuantity;
            exchangeVariant.StockQuantity = Math.Max(0, exchangeVariant.StockQuantity - model.Quantity);
            exchangeVariant.UpdatedAt = now;

            db.InventoryTransactions.Add(new InventoryTransaction
            {
                TxCode = $"RMA-OUT-{returnCode}-{Random.Shared.Next(100, 999)}",
                ProductVariantId = exchangeVariant.Id,
                TxType = "EXCHANGE_OUT",
                QuantityChange = -model.Quantity,
                QuantityBefore = oldExchangeStock,
                QuantityAfter = exchangeVariant.StockQuantity,
                ReferenceType = "OrderReturn",
                ReferenceId = order.Id,
                CreatedAt = now,
                Note = $"Đổi hàng: Xuất hàng mới (RMA {returnCode})"
            });

            // 3. Price difference
            var priceDifference = (exchangeVariant.Product.RetailPrice - originalItem.UnitPrice) * model.Quantity;
            if (priceDifference > 0)
            {
                // Surcharge collected from customer
                db.CashFlowLedger.Add(new CashFlowLedger
                {
                    VoucherCode = $"PT-{returnCode}",
                    VoucherType = "RECEIPT",
                    Category = "EXCHANGE_SURCHARGE",
                    Amount = priceDifference,
                    PaymentMethod = "Tiền mặt",
                    ReferenceType = "OrderReturn",
                    ReferenceId = order.Id,
                    OrderId = order.Id,
                    CreatedAt = now,
                    Note = $"Thu thêm chênh lệch giá đổi size ({returnCode})"
                });
            }
            else if (priceDifference < 0)
            {
                // Refund partial excess to customer
                var refundDiff = Math.Abs(priceDifference);
                db.CashFlowLedger.Add(new CashFlowLedger
                {
                    VoucherCode = $"PC-{returnCode}",
                    VoucherType = "PAYMENT",
                    Category = "EXCHANGE_REFUND",
                    Amount = refundDiff,
                    PaymentMethod = "Tiền mặt",
                    ReferenceType = "OrderReturn",
                    ReferenceId = order.Id,
                    OrderId = order.Id,
                    CreatedAt = now,
                    Note = $"Hoàn tiền chênh lệch đổi size ({returnCode})"
                });
            }

            var orderReturn = new OrderReturn
            {
                ReturnCode = returnCode,
                OrderId = order.Id,
                ReturnType = "EXCHANGE",
                Reason = model.Reason ?? "Khách đổi size",
                RefundAmount = priceDifference < 0 ? Math.Abs(priceDifference) : 0m,
                Status = "Completed",
                CreatedAt = now,
                CompletedAt = now
            };
            orderReturn.Items.Add(new OrderReturnItem
            {
                OriginalVariantId = originalVariant.Id,
                ExchangeVariantId = exchangeVariant.Id,
                Quantity = model.Quantity,
                RefundUnitPrice = originalItem.UnitPrice,
                IsResellable = model.IsResellable
            });

            db.OrderReturns.Add(orderReturn);
            await db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Xử lý đổi size thành công cho đơn {order.OrderCode} (Mã: {returnCode}).";
        }
        else // REFUND
        {
            var refundAmount = model.RefundAmount > 0 ? model.RefundAmount : (originalItem.UnitPrice * model.Quantity);

            // 1. Restock if resellable
            if (model.IsResellable)
            {
                var oldStock = originalVariant.StockQuantity;
                originalVariant.StockQuantity += model.Quantity;
                originalVariant.UpdatedAt = now;

                db.InventoryTransactions.Add(new InventoryTransaction
                {
                    TxCode = $"RMA-IN-{returnCode}-{Random.Shared.Next(100, 999)}",
                    ProductVariantId = originalVariant.Id,
                    TxType = "RETURN",
                    QuantityChange = model.Quantity,
                    QuantityBefore = oldStock,
                    QuantityAfter = originalVariant.StockQuantity,
                    ReferenceType = "OrderReturn",
                    ReferenceId = order.Id,
                    CreatedAt = now,
                    Note = $"Trả hàng: Nhập lại kho (RMA {returnCode})"
                });
            }

            // 2. Cash payment voucher
            db.CashFlowLedger.Add(new CashFlowLedger
            {
                VoucherCode = $"PC-{returnCode}",
                VoucherType = "PAYMENT",
                Category = "CUSTOMER_REFUND",
                Amount = refundAmount,
                PaymentMethod = "Chuyển khoản",
                ReferenceType = "OrderReturn",
                ReferenceId = order.Id,
                OrderId = order.Id,
                CreatedAt = now,
                Note = $"Chi hoàn tiền trả hàng cho đơn {order.OrderCode} ({returnCode})"
            });

            order.PaymentStatus = "Refunded";

            var orderReturn = new OrderReturn
            {
                ReturnCode = returnCode,
                OrderId = order.Id,
                ReturnType = "REFUND",
                Reason = model.Reason ?? "Khách trả hàng hoàn tiền",
                RefundAmount = refundAmount,
                Status = "Completed",
                CreatedAt = now,
                CompletedAt = now
            };
            orderReturn.Items.Add(new OrderReturnItem
            {
                OriginalVariantId = originalVariant.Id,
                ExchangeVariantId = null,
                Quantity = model.Quantity,
                RefundUnitPrice = originalItem.UnitPrice,
                IsResellable = model.IsResellable
            });

            db.OrderReturns.Add(orderReturn);
            await db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Xử lý trả hàng hoàn tiền thành công ({refundAmount:N0} đ, Mã: {returnCode}).";
        }

        return RedirectToAction(nameof(Returns));
    }

    // ==========================================
    // 4. PURCHASE ORDERS & INCOMING STOCK (PO)
    // ==========================================
    [HttpGet]
    public async Task<IActionResult> PurchaseOrders(string? search, string? status, int page = 1)
    {
        ViewData["ActiveMenu"] = "PurchaseOrders";
        const int pageSize = 15;

        var query = db.PurchaseOrders
            .Include(p => p.Supplier)
            .Include(p => p.Items)
                .ThenInclude(i => i.ProductVariant)
                    .ThenInclude(v => v.Product)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(p => p.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim().ToLower();
            query = query.Where(p => p.PoCode.ToLower().Contains(kw) ||
                                     p.Supplier.Name.ToLower().Contains(kw));
        }

        var totalItems = await query.CountAsync();
        var purchaseOrders = await query
            .OrderByDescending(p => p.OrderDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.CurrentStatus = status ?? "All";
        ViewBag.SearchQuery = search ?? "";
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        ViewBag.TotalCount = totalItems;

        return View(purchaseOrders);
    }

    [HttpGet]
    public async Task<IActionResult> CreatePurchaseOrder(int? preselectVariantId)
    {
        ViewData["ActiveMenu"] = "PurchaseOrders";

        ViewBag.Suppliers = await db.Suppliers
            .OrderBy(s => s.Name)
            .ToListAsync();

        var variants = await db.ProductVariants
            .Include(v => v.Product)
            .OrderBy(v => v.Product.Name)
            .ThenBy(v => v.Size)
            .Select(v => new
            {
                v.Id,
                v.VariantSku,
                ProductName = v.Product.Name,
                Brand = v.Product.Brand,
                v.Size,
                v.Color,
                CostPrice = v.CostPrice > 0 ? v.CostPrice : decimal.Round(v.Product.RetailPrice * 0.70m, 2),
                v.StockQuantity,
                v.MinStockAlert
            })
            .ToListAsync();

        ViewBag.Variants = variants;
        ViewBag.PreselectVariantId = preselectVariantId ?? 0;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePurchaseOrder(CreatePurchaseOrderInputModel model)
    {
        if (model.SupplierId <= 0 || model.Items == null || model.Items.Count == 0)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn nhà cung cấp và ít nhất một sản phẩm nhập.";
            return RedirectToAction(nameof(CreatePurchaseOrder));
        }

        var supplier = await db.Suppliers.FindAsync(model.SupplierId);
        if (supplier == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy nhà cung cấp.";
            return RedirectToAction(nameof(CreatePurchaseOrder));
        }

        var validItems = model.Items.Where(i => i.ProductVariantId > 0 && i.Quantity > 0 && i.UnitCost > 0).ToList();
        if (validItems.Count == 0)
        {
            TempData["ErrorMessage"] = "Dữ liệu mặt hàng nhập không hợp lệ (số lượng và đơn giá phải lớn hơn 0).";
            return RedirectToAction(nameof(CreatePurchaseOrder));
        }

        var totalAmount = validItems.Sum(i => i.Quantity * i.UnitCost);
        var paidAmount = Math.Clamp(model.PaidAmount, 0, totalAmount);
        var now = DateTime.UtcNow;
        var poCode = $"PO-{now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";

        var paymentStatus = paidAmount switch
        {
            _ when paidAmount >= totalAmount => "Paid",
            _ when paidAmount > 0 => "Partial",
            _ => "Unpaid"
        };

        var purchaseOrder = new PurchaseOrder
        {
            PoCode = poCode,
            SupplierId = supplier.Id,
            TotalAmount = totalAmount,
            PaidAmount = paidAmount,
            PaymentStatus = paymentStatus,
            Status = model.ReceiveImmediately ? "Received" : "Draft",
            OrderDate = now,
            ReceivedDate = model.ReceiveImmediately ? now : null
        };

        foreach (var item in validItems)
        {
            purchaseOrder.Items.Add(new PurchaseOrderItem
            {
                ProductVariantId = item.ProductVariantId,
                Quantity = item.Quantity,
                UnitCost = item.UnitCost,
                TotalCost = item.Quantity * item.UnitCost
            });
        }

        // Add unpaid balance to supplier debt
        var unpaidDebt = totalAmount - paidAmount;
        supplier.CurrentDebt += unpaidDebt;

        db.PurchaseOrders.Add(purchaseOrder);
        await db.SaveChangesAsync();

        // If paid something immediately, record payment & cash outflow
        if (paidAmount > 0)
        {
            var paymentCode = $"SP-{now:yyyyMMdd}-{purchaseOrder.Id}";
            var supplierPayment = new SupplierPayment
            {
                PaymentCode = paymentCode,
                SupplierId = supplier.Id,
                PurchaseOrderId = purchaseOrder.Id,
                Amount = paidAmount,
                PaymentMethod = model.PaymentMethod ?? "Chuyển khoản",
                PaidAt = now,
                Note = $"Thanh toán trước cho phiếu nhập {poCode}"
            };
            db.SupplierPayments.Add(supplierPayment);
            await db.SaveChangesAsync();

            db.CashFlowLedger.Add(new CashFlowLedger
            {
                VoucherCode = $"PC-{poCode}",
                VoucherType = "PAYMENT",
                Category = "SUPPLIER_PAYMENT",
                Amount = paidAmount,
                PaymentMethod = model.PaymentMethod ?? "Chuyển khoản",
                ReferenceType = "PurchaseOrder",
                ReferenceId = purchaseOrder.Id,
                PurchaseOrderId = purchaseOrder.Id,
                SupplierPaymentId = supplierPayment.Id,
                CreatedAt = now,
                Note = $"Chi thanh toán nhập hàng cho NCC {supplier.Name} ({poCode})"
            });
        }

        // If receive immediately, update stock and weighted average cost
        if (model.ReceiveImmediately)
        {
            foreach (var item in validItems)
            {
                var variant = await db.ProductVariants.FindAsync(item.ProductVariantId);
                if (variant != null)
                {
                    var oldStock = variant.StockQuantity;
                    var oldCost = variant.CostPrice;
                    var newStock = oldStock + item.Quantity;

                    // Weighted average cost formula: ((oldStock * oldCost) + (qty * unitCost)) / newStock
                    decimal newAvgCost;
                    if (newStock > 0)
                    {
                        newAvgCost = decimal.Round(((oldStock * oldCost) + (item.Quantity * item.UnitCost)) / newStock, 2);
                    }
                    else
                    {
                        newAvgCost = item.UnitCost;
                    }

                    variant.StockQuantity = newStock;
                    variant.CostPrice = newAvgCost;
                    variant.UpdatedAt = now;

                    db.InventoryTransactions.Add(new InventoryTransaction
                    {
                        TxCode = $"IMP-{poCode}-{variant.Id}",
                        ProductVariantId = variant.Id,
                        TxType = "IMPORT",
                        QuantityChange = item.Quantity,
                        QuantityBefore = oldStock,
                        QuantityAfter = newStock,
                        ReferenceType = "PurchaseOrder",
                        ReferenceId = purchaseOrder.Id,
                        CreatedAt = now,
                        Note = $"Nhập kho từ PO {poCode} (Giá vốn BQGQ mới: {newAvgCost:N0} đ)"
                    });
                }
            }
        }

        await db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Tạo phiếu nhập hàng {poCode} thành công. Tổng tiền: {totalAmount:N0} đ.";
        return RedirectToAction(nameof(PurchaseOrders));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReceivePurchaseOrder(int poId, string? returnUrl)
    {
        var po = await db.PurchaseOrders
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == poId);

        if (po == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy phiếu nhập hàng.";
            return RedirectToLocal(returnUrl);
        }

        if (po.Status == "Received")
        {
            TempData["ErrorMessage"] = "Phiếu nhập hàng này đã được xác nhận nhập kho trước đó.";
            return RedirectToLocal(returnUrl);
        }

        var now = DateTime.UtcNow;
        po.Status = "Received";
        po.ReceivedDate = now;

        foreach (var item in po.Items)
        {
            var variant = await db.ProductVariants.FindAsync(item.ProductVariantId);
            if (variant != null)
            {
                var oldStock = variant.StockQuantity;
                var oldCost = variant.CostPrice;
                var newStock = oldStock + item.Quantity;

                decimal newAvgCost = newStock > 0
                    ? decimal.Round(((oldStock * oldCost) + (item.Quantity * item.UnitCost)) / newStock, 2)
                    : item.UnitCost;

                variant.StockQuantity = newStock;
                variant.CostPrice = newAvgCost;
                variant.UpdatedAt = now;

                db.InventoryTransactions.Add(new InventoryTransaction
                {
                    TxCode = $"IMP-{po.Id}-{variant.Id}-{now:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}",
                    ProductVariantId = variant.Id,
                    TxType = "IMPORT",
                    QuantityChange = item.Quantity,
                    QuantityBefore = oldStock,
                    QuantityAfter = newStock,
                    ReferenceType = "PurchaseOrder",
                    ReferenceId = po.Id,
                    CreatedAt = now,
                    Note = $"Nhập kho xác nhận PO {po.PoCode} (Giá vốn BQGQ: {newAvgCost:N0} đ)"
                });
            }
        }

        await db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Xác nhận nhập kho thành công cho phiếu {po.PoCode}. Tồn kho và giá vốn đã được cập nhật.";
        return RedirectToLocal(returnUrl);
    }

    // ==========================================
    // 5. SUPPLIERS & DEBT PAYMENTS
    // ==========================================
    [HttpGet]
    public async Task<IActionResult> Suppliers(string? search, int page = 1)
    {
        ViewData["ActiveMenu"] = "Suppliers";
        const int pageSize = 15;

        var query = db.Suppliers
            .Include(s => s.PurchaseOrders)
            .Include(s => s.SupplierPayments)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim().ToLower();
            query = query.Where(s => s.Name.ToLower().Contains(kw) ||
                                     s.Code.ToLower().Contains(kw) ||
                                     (s.Phone != null && s.Phone.ToLower().Contains(kw)));
        }

        var totalItems = await query.CountAsync();
        var suppliers = await query
            .OrderByDescending(s => s.CurrentDebt)
            .ThenBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.SearchQuery = search ?? "";
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        ViewBag.TotalCount = totalItems;
        ViewBag.TotalDebt = await db.Suppliers.SumAsync(s => (decimal?)s.CurrentDebt) ?? 0m;

        // Recent payments for ledger view
        ViewBag.RecentPayments = await db.SupplierPayments
            .Include(p => p.Supplier)
            .Include(p => p.PurchaseOrder)
            .OrderByDescending(p => p.PaidAt)
            .Take(10)
            .ToListAsync();

        return View(suppliers);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSupplier(string code, string name, string? phone, string? email, string? address)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            TempData["ErrorMessage"] = "Mã và Tên nhà cung cấp không được để trống.";
            return RedirectToAction(nameof(Suppliers));
        }

        var existing = await db.Suppliers.AnyAsync(s => s.Code == code.Trim().ToUpper());
        if (existing)
        {
            TempData["ErrorMessage"] = $"Mã nhà cung cấp '{code}' đã tồn tại.";
            return RedirectToAction(nameof(Suppliers));
        }

        var supplier = new Supplier
        {
            Code = code.Trim().ToUpper(),
            Name = name.Trim(),
            Phone = phone?.Trim(),
            Email = email?.Trim(),
            Address = address?.Trim(),
            CurrentDebt = 0m
        };

        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Thêm nhà cung cấp '{supplier.Name}' thành công.";
        return RedirectToAction(nameof(Suppliers));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSupplierPayment(int supplierId, int? purchaseOrderId, decimal amount, string paymentMethod, string? note)
    {
        if (supplierId <= 0 || amount <= 0)
        {
            TempData["ErrorMessage"] = "Nhà cung cấp hoặc số tiền chi trả không hợp lệ.";
            return RedirectToAction(nameof(Suppliers));
        }

        var supplier = await db.Suppliers.FindAsync(supplierId);
        if (supplier == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy nhà cung cấp.";
            return RedirectToAction(nameof(Suppliers));
        }

        var now = DateTime.UtcNow;
        var paymentCode = $"SP-{now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";

        // Reduce debt
        supplier.CurrentDebt = Math.Max(0, supplier.CurrentDebt - amount);

        // If specific PO selected, update its paid amount
        PurchaseOrder? po = null;
        if (purchaseOrderId.HasValue && purchaseOrderId.Value > 0)
        {
            po = await db.PurchaseOrders.FindAsync(purchaseOrderId.Value);
            if (po != null)
            {
                po.PaidAmount += amount;
                po.PaymentStatus = po.PaidAmount >= po.TotalAmount ? "Paid" : "Partial";
            }
        }

        var payment = new SupplierPayment
        {
            PaymentCode = paymentCode,
            SupplierId = supplier.Id,
            PurchaseOrderId = po?.Id,
            Amount = amount,
            PaymentMethod = string.IsNullOrWhiteSpace(paymentMethod) ? "Chuyển khoản" : paymentMethod,
            PaidAt = now,
            Note = note ?? $"Trả nợ nhà cung cấp {supplier.Name}"
        };

        db.SupplierPayments.Add(payment);
        await db.SaveChangesAsync();

        // Record in cash flow ledger (Payment outflow)
        db.CashFlowLedger.Add(new CashFlowLedger
        {
            VoucherCode = $"PC-{paymentCode}",
            VoucherType = "PAYMENT",
            Category = "SUPPLIER_PAYMENT",
            Amount = amount,
            PaymentMethod = payment.PaymentMethod,
            ReferenceType = "SupplierPayment",
            ReferenceId = payment.Id,
            SupplierPaymentId = payment.Id,
            PurchaseOrderId = po?.Id,
            CreatedAt = now,
            Note = $"Chi trả nợ NCC {supplier.Name}" + (po != null ? $" (PO: {po.PoCode})" : "")
        });

        await db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Ghi nhận phiếu chi trả nợ {amount:N0} đ cho NCC {supplier.Name} thành công.";
        return RedirectToAction(nameof(Suppliers));
    }

    [HttpGet]
    public async Task<IActionResult> GetSupplierUnpaidPOs(int supplierId)
    {
        var pos = await db.PurchaseOrders
            .Where(p => p.SupplierId == supplierId && p.PaymentStatus != "Paid")
            .Select(p => new
            {
                p.Id,
                p.PoCode,
                p.TotalAmount,
                p.PaidAmount,
                Remaining = p.TotalAmount - p.PaidAmount
            })
            .ToListAsync();

        return Json(pos);
    }

    // ==========================================
    // 6. INVENTORY & LOW STOCK ALERTS
    // ==========================================
    [HttpGet]
    public async Task<IActionResult> Inventory(string? search, string? brand, bool onlyLowStock = false, int page = 1)
    {
        ViewData["ActiveMenu"] = "Inventory";
        const int pageSize = 20;

        var query = db.ProductVariants
            .Include(v => v.Product)
            .AsQueryable();

        if (onlyLowStock)
        {
            query = query.Where(v => v.StockQuantity <= v.MinStockAlert);
        }

        if (!string.IsNullOrWhiteSpace(brand) && !brand.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(v => v.Product.Brand == brand);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim().ToLower();
            query = query.Where(v => v.Product.Name.ToLower().Contains(kw) ||
                                     v.VariantSku.ToLower().Contains(kw) ||
                                     v.Color.ToLower().Contains(kw));
        }

        var totalItems = await query.CountAsync();
        var variants = await query
            .OrderBy(v => v.StockQuantity <= v.MinStockAlert ? 0 : 1)
            .ThenBy(v => v.StockQuantity)
            .ThenBy(v => v.Product.Name)
            .ThenBy(v => v.Size)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Summary counts
        ViewBag.TotalVariantCount = await db.ProductVariants.CountAsync();
        ViewBag.TotalStockPieces = await db.ProductVariants.SumAsync(v => (int?)v.StockQuantity) ?? 0;
        ViewBag.TotalReservedPieces = await db.ProductVariants.SumAsync(v => (int?)v.ReservedQuantity) ?? 0;
        ViewBag.LowStockCount = await db.ProductVariants.CountAsync(v => v.StockQuantity <= v.MinStockAlert);
        ViewBag.OutOfStockCount = await db.ProductVariants.CountAsync(v => v.StockQuantity == 0);

        // Brands list for filter
        ViewBag.Brands = await db.Products.Select(p => p.Brand).Distinct().ToListAsync();

        ViewBag.SearchQuery = search ?? "";
        ViewBag.CurrentBrand = brand ?? "All";
        ViewBag.OnlyLowStock = onlyLowStock;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        ViewBag.TotalCount = totalItems;

        // Recent inventory movements (last 10)
        ViewBag.RecentTransactions = await db.InventoryTransactions
            .Include(t => t.ProductVariant)
                .ThenInclude(v => v.Product)
            .OrderByDescending(t => t.CreatedAt)
            .Take(10)
            .ToListAsync();

        return View(variants);
    }

    // ==========================================
    // 7. CASH FLOW LEDGER (SỔ QUỸ THỰC TẾ)
    // ==========================================
    [HttpGet]
    public async Task<IActionResult> CashFlow(string? voucherType, string? category, int page = 1)
    {
        ViewData["ActiveMenu"] = "CashFlow";
        const int pageSize = 20;

        var query = db.CashFlowLedger
            .Include(c => c.Order)
            .Include(c => c.PurchaseOrder)
            .Include(c => c.SupplierPayment)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(voucherType) && !voucherType.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(c => c.VoucherType == voucherType);
        }

        if (!string.IsNullOrWhiteSpace(category) && !category.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(c => c.Category == category);
        }

        var totalItems = await query.CountAsync();
        var ledgerEntries = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var totalInflow = await db.CashFlowLedger
            .Where(c => c.VoucherType == "RECEIPT")
            .SumAsync(c => (decimal?)c.Amount) ?? 0m;

        var totalOutflow = await db.CashFlowLedger
            .Where(c => c.VoucherType == "PAYMENT")
            .SumAsync(c => (decimal?)c.Amount) ?? 0m;

        ViewBag.TotalInflow = totalInflow;
        ViewBag.TotalOutflow = totalOutflow;
        ViewBag.CurrentBalance = totalInflow - totalOutflow;
        ViewBag.CurrentVoucherType = voucherType ?? "All";
        ViewBag.CurrentCategory = category ?? "All";
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        ViewBag.TotalCount = totalItems;

        return View(ledgerEntries);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCashFlowEntry(string voucherType, string category, decimal amount, string paymentMethod, string? note)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(voucherType) || string.IsNullOrWhiteSpace(category))
        {
            TempData["ErrorMessage"] = "Thông tin phiếu thu chi không hợp lệ.";
            return RedirectToAction(nameof(CashFlow));
        }

        var now = DateTime.UtcNow;
        var prefix = voucherType == "RECEIPT" ? "PT" : "PC";
        var voucherCode = $"{prefix}-MAN-{now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";

        var entry = new CashFlowLedger
        {
            VoucherCode = voucherCode,
            VoucherType = voucherType,
            Category = category,
            Amount = amount,
            PaymentMethod = string.IsNullOrWhiteSpace(paymentMethod) ? "Tiền mặt" : paymentMethod,
            ReferenceType = "Manual",
            ReferenceId = 0,
            CreatedAt = now,
            Note = note ?? $"Phiếu {(voucherType == "RECEIPT" ? "thu" : "chi")} thủ công ({category})"
        };

        db.CashFlowLedger.Add(entry);
        await db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã lập phiếu {voucherCode} thành công. Số tiền: {amount:N0} đ.";
        return RedirectToAction(nameof(CashFlow));
    }

    // Helper redirect
    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction(nameof(Orders));
    }
}

// ==========================================
// VIEW MODELS
// ==========================================

public class AdminDashboardViewModel
{
    public decimal NetRevenue { get; set; }
    public decimal Cogs { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal NetCashBalance { get; set; }
    public decimal TotalCashInflow { get; set; }
    public decimal TotalCashOutflow { get; set; }
    public int PendingOrdersCount { get; set; }
    public decimal TotalSupplierDebt { get; set; }
    public int LowStockVariantsCount { get; set; }
    public List<LowStockItemViewModel> LowStockAlerts { get; set; } = new();
    public List<Order> RecentOrders { get; set; } = new();
}

public class LowStockItemViewModel
{
    public int VariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public int Size { get; set; }
    public string Color { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public int MinStockAlert { get; set; }
    public decimal CostPrice { get; set; }
    public decimal RetailPrice { get; set; }
}

public class CreateReturnInputModel
{
    public int OrderId { get; set; }
    public int OriginalVariantId { get; set; }
    public int? ExchangeVariantId { get; set; }
    public int Quantity { get; set; } = 1;
    public string ReturnType { get; set; } = "EXCHANGE"; // EXCHANGE or REFUND
    public decimal RefundAmount { get; set; }
    public string? Reason { get; set; }
    public bool IsResellable { get; set; } = true;
}

public class CreatePurchaseOrderInputModel
{
    public int SupplierId { get; set; }
    public decimal PaidAmount { get; set; }
    public string? PaymentMethod { get; set; } = "Chuyển khoản";
    public bool ReceiveImmediately { get; set; } = true;
    public List<PurchaseOrderItemInputModel> Items { get; set; } = new();
}

public class PurchaseOrderItemInputModel
{
    public int ProductVariantId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
}
