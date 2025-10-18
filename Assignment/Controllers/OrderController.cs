using Assignment.Data;
using Assignment.Models;
using Assignment.Enums;
using Assignment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Assignment.Extensions;
using System;
using System.Linq;
using System.Collections.Generic;
using Assignment.Services.Payments;
using Microsoft.Extensions.Logging;

namespace Assignment.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPayOsService _payOsService;
        private readonly ILogger<OrderController> _logger;

        public OrderController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IPayOsService payOsService, ILogger<OrderController> logger)
        {
            _context = context;
            _userManager = userManager;
            _payOsService = payOsService;
            _logger = logger;
        }

        public async Task<IActionResult> Checkout(string? cartItemIds)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var cart = await _context.LoadCartWithAvailableItemsAsync(userId);

            if (cart == null || !cart.CartItems.Any())
            {
                TempData["Error"] = "Giỏ hàng của bạn đang trống. Vui lòng thêm sản phẩm trước khi thanh toán.";
                return RedirectToAction("Index", "Cart");
            }

            var selectedIds = ParseSelectedCartItemIds(cartItemIds);
            var filteredItems = FilterCartItems(cart.CartItems, selectedIds);

            if (!filteredItems.Any())
            {
                TempData["Error"] = "Các sản phẩm đã chọn không còn khả dụng.";
                return RedirectToAction("Index", "Cart");
            }

            var user = await _userManager.GetUserAsync(User);
            var order = new Order
            {
                Name = !string.IsNullOrWhiteSpace(user?.FullName)
                    ? user.FullName
                    : User.Identity?.Name ?? string.Empty,
                Email = user?.Email,
                Phone = user?.PhoneNumber ?? string.Empty,
                UserId = userId,
                SelectedCartItemIds = string.Join(',', filteredItems.Select(ci => ci.Id))
            };

            cart.CartItems = filteredItems;
            ViewBag.Cart = cart;
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(Order order)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var cart = await _context.LoadCartWithAvailableItemsAsync(userId);

            if (cart == null || !cart.CartItems.Any())
            {
                ModelState.AddModelError("", "Giỏ hàng của bạn trống.");
                ViewBag.Cart = cart;
                return View(order);
            }

            var selectedIds = ParseSelectedCartItemIds(order.SelectedCartItemIds);
            var filteredItems = FilterCartItems(cart.CartItems, selectedIds);

            if (!filteredItems.Any())
            {
                TempData["Error"] = "Các sản phẩm đã chọn không còn khả dụng.";
                return RedirectToAction("Index", "Cart");
            }

            ModelState.Remove("Voucher");
            ModelState.Remove("OrderItems");

            if (order.PaymentType == PaymentType.PayNow)
            {
                if (!order.PaymentMethod.HasValue)
                {
                    ModelState.AddModelError("PaymentMethod", "Vui lòng chọn cổng thanh toán hợp lệ.");
                }
                else if (order.PaymentMethod != PaymentMethodType.Bank)
                {
                    ModelState.AddModelError("PaymentMethod", "Hiện tại chỉ hỗ trợ thanh toán PayNow thông qua ngân hàng.");
                }
            }
            else
            {
                order.PaymentMethod = null;
            }

            order.SelectedCartItemIds = string.Join(',', filteredItems.Select(ci => ci.Id));

            long? voucherId = null;
            if (!string.IsNullOrEmpty(order.VoucherId) && long.TryParse(order.VoucherId, out var parsedId))
            {
                voucherId = parsedId;
            }

            if (ModelState.IsValid)
            {
                order.UserId = userId;
                order.CreatedAt = DateTime.Now;
                order.Status = OrderStatus.Pending;

                var orderItems = new List<OrderItem>();
                foreach (var cartItem in filteredItems)
                {
                    var unitPrice = GetCartItemUnitPrice(cartItem);
                    orderItems.Add(new OrderItem
                    {
                        Price = unitPrice,
                        Quantity = cartItem.Quantity,
                        ProductId = cartItem.ProductId,
                        ComboId = cartItem.ComboId,
                        Product = cartItem.Product,
                        Combo = cartItem.Combo
                    });
                }

                order.OrderItems = orderItems;
                order.TotalQuantity = orderItems.Sum(oi => oi.Quantity);
                order.TotalPrice = orderItems.Sum(oi => oi.Price * oi.Quantity);
                order.Discount = 0;

                if (voucherId.HasValue)
                {
                    var voucher = await _context.Vouchers
                        .Include(v => v.VoucherUsers)
                        .FirstOrDefaultAsync(v => v.Id == voucherId.Value);
                    var now = DateTime.Now;

                    if (voucher != null)
                    {
                        var allowedUserIds = voucher.VoucherUsers?
                            .Where(vu => !vu.IsDeleted)
                            .Select(vu => vu.UserId)
                            .ToHashSet() ?? new HashSet<string>();

                        if (!string.IsNullOrWhiteSpace(voucher.UserId))
                        {
                            allowedUserIds.Add(voucher.UserId);
                        }

                        var isPrivateVoucherAllowed = voucher.Type != VoucherType.Private ||
                            (userId != null && allowedUserIds.Contains(userId));

                        if (voucher.StartTime <= now &&
                            (!voucher.EndTime.HasValue || voucher.EndTime.Value >= now || voucher.IsLifeTime) &&
                            voucher.Quantity > voucher.Used &&
                            isPrivateVoucherAllowed &&
                            order.TotalPrice >= voucher.MinimumRequirements)
                        {
                            double discountAmount = 0;
                            if (voucher.DiscountType == VoucherDiscountType.Money)
                            {
                                discountAmount = voucher.Discount;
                            }
                            else
                            {
                                discountAmount = order.TotalPrice * (voucher.Discount / 100);
                                if (!voucher.UnlimitedPercentageDiscount && voucher.MaximumPercentageReduction.HasValue && discountAmount > voucher.MaximumPercentageReduction.Value)
                                {
                                    discountAmount = voucher.MaximumPercentageReduction.Value;
                                }
                            }
                            order.Discount = Math.Min(discountAmount, order.TotalPrice);
                            order.VoucherId = voucher.Id.ToString();

                            voucher.Used += 1;
                            _context.Vouchers.Update(voucher);
                        }
                        else
                        {
                            order.VoucherId = null;
                        }
                    }
                }

                double priceAfterDiscount = order.TotalPrice - order.Discount;
                order.Vat = priceAfterDiscount * 0.15;
                order.TotalBill = priceAfterDiscount + order.Vat;

                _context.Orders.Add(order);
                _context.CartItems.RemoveRange(filteredItems);
                await _context.SaveChangesAsync();

                if (order.PaymentType == PaymentType.PayNow && order.PaymentMethod == PaymentMethodType.Bank)
                {
                    var returnUrl = Url.Action(nameof(PayOsReturn), "Order", new { orderId = order.Id }, Request.Scheme) ?? string.Empty;
                    var cancelUrl = Url.Action(nameof(PayOsCancel), "Order", new { orderId = order.Id }, Request.Scheme) ?? string.Empty;
                    var description = $"Thanh toán đơn hàng #{order.Id}";

                    try
                    {
                        var paymentLink = await _payOsService.CreatePaymentLinkAsync(order, description, returnUrl, cancelUrl);
                        if (paymentLink != null && !string.IsNullOrWhiteSpace(paymentLink.CheckoutUrl))
                        {
                            return Redirect(paymentLink.CheckoutUrl);
                        }

                        TempData["Error"] = "Không thể tạo liên kết thanh toán PayOS. Đơn hàng của bạn vẫn đang chờ thanh toán.";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Không thể tạo thanh toán PayOS cho đơn hàng {OrderId}", order.Id);
                        TempData["Error"] = "Có lỗi xảy ra khi kết nối với PayOS. Đơn hàng của bạn vẫn đang chờ thanh toán.";
                    }
                }

                return RedirectToAction(nameof(OrderConfirmation), new { id = order.Id });
            }

            cart.CartItems = filteredItems;
            ViewBag.Cart = cart;
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyVoucher(string voucherCode, string? selectedCartItemIds)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var cart = await _context.LoadCartWithAvailableItemsAsync(userId);

            if (cart == null || !cart.CartItems.Any())
            {
                return Json(new { success = false, error = "Giỏ hàng của bạn đang trống." });
            }

            var voucher = await _context.Vouchers
                .Include(v => v.VoucherUsers)
                .FirstOrDefaultAsync(v => v.Code.ToUpper() == voucherCode.ToUpper());
            var now = DateTime.Now;

            if (voucher == null) return Json(new { success = false, error = "Mã voucher không hợp lệ." });
            if (voucher.StartTime > now || (voucher.EndTime.HasValue && voucher.EndTime < now && !voucher.IsLifeTime)) return Json(new { success = false, error = "Voucher đã hết hạn hoặc chưa có hiệu lực." });
            if (voucher.Quantity <= voucher.Used) return Json(new { success = false, error = "Voucher đã hết lượt sử dụng." });
            var allowedUserIds = voucher.VoucherUsers?
                .Where(vu => !vu.IsDeleted)
                .Select(vu => vu.UserId)
                .ToHashSet() ?? new HashSet<string>();
            if (!string.IsNullOrWhiteSpace(voucher.UserId))
            {
                allowedUserIds.Add(voucher.UserId);
            }
            if (voucher.Type == VoucherType.Private && (userId == null || !allowedUserIds.Contains(userId))) return Json(new { success = false, error = "Bạn không thể sử dụng voucher này." });

            var selectedIds = ParseSelectedCartItemIds(selectedCartItemIds);
            var filteredItems = FilterCartItems(cart.CartItems, selectedIds);

            if (!filteredItems.Any())
            {
                return Json(new { success = false, error = "Các sản phẩm đã chọn không còn khả dụng." });
            }

            var subtotal = filteredItems.Sum(item => GetCartItemUnitPrice(item) * item.Quantity);
            if (subtotal < voucher.MinimumRequirements) return Json(new { success = false, error = $"Đơn hàng tối thiểu phải là {voucher.MinimumRequirements:N0}đ." });

            double discountAmount = 0;
            if (voucher.DiscountType == VoucherDiscountType.Money)
            {
                discountAmount = voucher.Discount;
            }
            else
            {
                discountAmount = subtotal * (voucher.Discount / 100);
                if (!voucher.UnlimitedPercentageDiscount && voucher.MaximumPercentageReduction.HasValue && discountAmount > voucher.MaximumPercentageReduction.Value)
                {
                    discountAmount = voucher.MaximumPercentageReduction.Value;
                }
            }

            discountAmount = Math.Min(discountAmount, subtotal);

            double priceAfterDiscount = subtotal - discountAmount;
            double vatAmount = priceAfterDiscount * 0.15;
            double totalBill = priceAfterDiscount + vatAmount;

            return Json(new
            {
                success = true,
                voucherId = voucher.Id.ToString(),
                discountAmount,
                vatAmount,
                totalBill
            });
        }


        public async Task<IActionResult> History()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var orders = await _context.Orders
                .Where(o => o.UserId == userId)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Combo)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View(orders);
        }

        public async Task<IActionResult> Manage(OrderStatus? status, string? search)
        {
            if (!CanManageOrders())
            {
                return Forbid();
            }

            var ordersQuery = _context.Orders
                .Where(o => !o.IsDeleted)
                .Include(o => o.OrderItems)!
                    .ThenInclude(oi => oi.Product)
                .Include(o => o.OrderItems)!
                    .ThenInclude(oi => oi.Combo)
                .AsQueryable();

            if (status.HasValue)
            {
                ordersQuery = ordersQuery.Where(o => o.Status == status.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = search.Trim();
                var hasId = long.TryParse(keyword, out var orderId);

                ordersQuery = ordersQuery.Where(o =>
                    EF.Functions.Like(o.Name, $"%{keyword}%") ||
                    (o.Email != null && EF.Functions.Like(o.Email, $"%{keyword}%")) ||
                    EF.Functions.Like(o.Phone, $"%{keyword}%") ||
                    (hasId && o.Id == orderId));
            }

            var orders = await ordersQuery
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            ViewBag.StatusFilter = status;
            ViewBag.SearchTerm = search;
            ViewBag.CanUpdateStatus = CanManageOrders();

            return View(orders);
        }

        public async Task<IActionResult> Details(long id)
        {
            if (!CanManageOrders())
            {
                return Forbid();
            }

            var order = await _context.Orders
                .Where(o => o.Id == id && !o.IsDeleted)
                .Include(o => o.OrderItems)!
                    .ThenInclude(oi => oi.Product)
                .Include(o => o.OrderItems)!
                    .ThenInclude(oi => oi.Combo)
                .FirstOrDefaultAsync();

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(long id, OrderStatus status, OrderStatus? statusFilter, string? search)
        {
            if (!CanManageOrders())
            {
                return Forbid();
            }

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted);
            if (order == null)
            {
                TempData["Error"] = "Không tìm thấy đơn hàng.";
                return RedirectToAction(nameof(Manage), new { status = statusFilter, search });
            }

            if (order.Status == status)
            {
                TempData["Info"] = "Đơn hàng đã ở trạng thái này.";
                return RedirectToAction(nameof(Manage), new { status = statusFilter, search });
            }

            order.Status = status;
            order.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Cập nhật trạng thái đơn hàng #{order.Id} thành công.";
            return RedirectToAction(nameof(Manage), new { status = statusFilter, search });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkUpdateStatus([FromForm] List<long> selectedIds, OrderStatus status, OrderStatus? statusFilter, string? search)
        {
            if (!CanManageOrders())
            {
                return Forbid();
            }

            if (selectedIds == null || selectedIds.Count == 0)
            {
                TempData["Info"] = "Vui lòng chọn ít nhất một đơn hàng để cập nhật.";
                return RedirectToAction(nameof(Manage), new { status = statusFilter, search });
            }

            var orders = await _context.Orders
                .Where(o => selectedIds.Contains(o.Id) && !o.IsDeleted)
                .ToListAsync();

            if (!orders.Any())
            {
                TempData["Info"] = "Không tìm thấy đơn hàng hợp lệ để cập nhật.";
                return RedirectToAction(nameof(Manage), new { status = statusFilter, search });
            }

            var now = DateTime.Now;
            var updatedCount = 0;
            var unchangedCount = 0;

            foreach (var order in orders)
            {
                if (order.Status == status)
                {
                    unchangedCount++;
                    continue;
                }

                order.Status = status;
                order.UpdatedAt = now;
                updatedCount++;
            }

            if (updatedCount > 0)
            {
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã cập nhật {updatedCount} đơn hàng.";
            }
            else
            {
                TempData["Info"] = "Không có đơn hàng nào được cập nhật.";
            }

            if (unchangedCount > 0)
            {
                var message = $"{unchangedCount} đơn hàng đã ở trạng thái được chọn.";
                var existingInfo = TempData.ContainsKey("Info") ? TempData["Info"]?.ToString() : null;
                TempData["Info"] = string.IsNullOrWhiteSpace(existingInfo)
                    ? message
                    : $"{existingInfo} {message}";
            }

            return RedirectToAction(nameof(Manage), new { status = statusFilter, search });
        }

        [HttpGet]
        public async Task<IActionResult> PayOsReturn(long orderId)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted);
            if (order == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.Equals(order.UserId, userId, StringComparison.Ordinal))
            {
                return Forbid();
            }

            if (order.PaymentType != PaymentType.PayNow || order.PaymentMethod != PaymentMethodType.Bank)
            {
                return RedirectToAction(nameof(OrderConfirmation), new { id = order.Id });
            }

            try
            {
                var details = await _payOsService.GetPaymentDetailsAsync(order.Id);
                if (details != null && string.Equals(details.Status, "PAID", StringComparison.OrdinalIgnoreCase))
                {
                    if (order.Status != OrderStatus.Paid)
                    {
                        order.Status = OrderStatus.Paid;
                        order.UpdatedAt = DateTime.Now;
                        await _context.SaveChangesAsync();
                    }
                }
                else
                {
                    TempData["Error"] = "Thanh toán PayOS chưa được xác nhận. Đơn hàng của bạn vẫn đang chờ thanh toán.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể xác minh thanh toán PayOS cho đơn hàng {OrderId}", order.Id);
                TempData["Error"] = "Không thể xác minh thanh toán PayOS. Vui lòng thử lại sau.";
            }

            return RedirectToAction(nameof(OrderConfirmation), new { id = order.Id });
        }

        [HttpGet]
        public async Task<IActionResult> PayOsCancel(long orderId)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted);
            if (order == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.Equals(order.UserId, userId, StringComparison.Ordinal))
            {
                return Forbid();
            }

            TempData["Error"] = "Thanh toán PayOS đã bị hủy hoặc không thành công. Đơn hàng của bạn vẫn đang chờ thanh toán.";
            return RedirectToAction(nameof(OrderConfirmation), new { id = orderId });
        }

        public async Task<IActionResult> OrderConfirmation(long id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Combo)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        private static ISet<long> ParseSelectedCartItemIds(string? selectedCartItemIds)
        {
            var result = new HashSet<long>();

            if (string.IsNullOrWhiteSpace(selectedCartItemIds))
            {
                return result;
            }

            var parts = selectedCartItemIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var part in parts)
            {
                if (long.TryParse(part, out var value) && value > 0)
                {
                    result.Add(value);
                }
            }

            return result;
        }

        private static List<CartItem> FilterCartItems(IEnumerable<CartItem> cartItems, ISet<long> selectedIds)
        {
            if (cartItems == null)
            {
                return new List<CartItem>();
            }

            if (selectedIds == null || selectedIds.Count == 0)
            {
                return cartItems.ToList();
            }

            return cartItems.Where(ci => selectedIds.Contains(ci.Id)).ToList();
        }

        private static double GetCartItemUnitPrice(CartItem cartItem)
        {
            if (cartItem.Product != null)
            {
                return PriceCalculator.GetProductFinalPrice(cartItem.Product);
            }

            if (cartItem.Combo != null)
            {
                return PriceCalculator.GetComboFinalPrice(cartItem.Combo);
            }

            return 0;
        }

        private bool CanManageOrders()
            => User.HasPermission("GetOrderAll");
    }
}

