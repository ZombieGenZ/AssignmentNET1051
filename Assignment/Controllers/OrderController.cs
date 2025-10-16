using Assignment.Data;
using Assignment.Models;
using Assignment.Enums;
using Assignment.Services;
using Assignment.Services.PayOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
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

        // GET: Order/Checkout
        public async Task<IActionResult> Checkout()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var cart = await _context.LoadCartWithAvailableItemsAsync(userId);

            if (cart == null || !cart.CartItems.Any())
            {
                TempData["Error"] = "Giỏ hàng của bạn đang trống. Vui lòng thêm sản phẩm trước khi thanh toán.";
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
                // You can pre-fill other fields like email/phone if they are in user claims
            };

            ViewBag.Cart = cart; // Pass cart data to the view for summary
            return View(order);
        }

        // POST: Order/Checkout
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

            long? voucherId = null;
            if (!string.IsNullOrEmpty(order.VoucherId) && long.TryParse(order.VoucherId, out var parsedId))
            {
                voucherId = parsedId;
            }

            if (ModelState.IsValid)
            {
                // Server-side calculation to ensure data integrity
                order.UserId = userId;
                order.CreatedAt = DateTime.Now;
                order.Status = OrderStatus.Pending;

                var orderItems = new List<OrderItem>();
                foreach (var cartItem in cart.CartItems)
                {
                    var unitPrice = GetCartItemUnitPrice(cartItem);
                    orderItems.Add(new OrderItem
                    {
                        Price = unitPrice,
                        Quantity = cartItem.Quantity,
                        ProductId = cartItem.ProductId,
                        ComboId = cartItem.ComboId
                    });
                }

                order.OrderItems = orderItems;
                order.TotalQuantity = orderItems.Sum(oi => oi.Quantity);
                order.TotalPrice = orderItems.Sum(oi => oi.Price * oi.Quantity);
                order.Discount = 0;

                // --- Re-validate and Apply Voucher before saving ---
                if (voucherId.HasValue)
                {
                    var voucher = await _context.Vouchers.FindAsync(voucherId.Value);
                    var now = DateTime.Now;
                    // Re-run validation checks
                    if (voucher != null &&
                        voucher.StartTime <= now &&
                        (!voucher.EndTime.HasValue || voucher.EndTime.Value >= now || voucher.IsLifeTime) &&
                        voucher.Quantity > voucher.Used &&
                        (voucher.Type != VoucherType.Private || voucher.UserId == userId) &&
                        order.TotalPrice >= voucher.MinimumRequirements)
                    {
                        double discountAmount = 0;
                        if (voucher.DiscountType == VoucherDiscountType.Money)
                        {
                            discountAmount = voucher.Discount;
                        }
                        else // Percent
                        {
                            discountAmount = order.TotalPrice * (voucher.Discount / 100);
                            if (!voucher.UnlimitedPercentageDiscount && voucher.MaximumPercentageReduction.HasValue && discountAmount > voucher.MaximumPercentageReduction.Value)
                            {
                                discountAmount = voucher.MaximumPercentageReduction.Value;
                            }
                        }
                        order.Discount = Math.Min(discountAmount, order.TotalPrice); // Ensure discount is not more than total
                        order.VoucherId = voucher.Id.ToString();

                        voucher.Used += 1; // Increment used count
                        _context.Vouchers.Update(voucher);
                    }
                    else
                    {
                        order.VoucherId = null; // Invalidate voucher if checks fail
                    }
                }

                // --- Final Calculations ---
                double priceAfterDiscount = order.TotalPrice - order.Discount;
                order.Vat = priceAfterDiscount * 0.15; // 15% VAT on the discounted price
                order.TotalBill = priceAfterDiscount + order.Vat;

                _context.Orders.Add(order);
                _context.CartItems.RemoveRange(cart.CartItems);
                await _context.SaveChangesAsync();

                if (order.PaymentType == PaymentType.PayNow && order.PaymentMethod == PaymentMethodType.Bank)
                {
                    var successUrl = Url.Action(nameof(PayOsReturn), "Order", new { id = order.Id }, Request.Scheme, Request.Host.ToString());
                    var cancelUrl = Url.Action(nameof(PayOsCancel), "Order", new { id = order.Id }, Request.Scheme, Request.Host.ToString());

                    if (string.IsNullOrWhiteSpace(successUrl) || string.IsNullOrWhiteSpace(cancelUrl))
                    {
                        _logger.LogWarning("Cannot build PayOS return or cancel URL for order {OrderId}.", order.Id);
                        TempData["Error"] = "Không thể xác định địa chỉ phản hồi từ PayOS. Vui lòng thử lại sau.";
                        return RedirectToAction(nameof(OrderConfirmation), new { id = order.Id });
                    }

                    try
                    {
                        var payUrl = await _payOsService.CreatePaymentUrlAsync(order, successUrl, cancelUrl);
                        return Redirect(payUrl);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unable to create PayOS payment link for order {OrderId}.", order.Id);
                        TempData["Error"] = "Không thể khởi tạo thanh toán PayOS. Vui lòng thử lại sau hoặc chọn phương thức thanh toán khác.";
                    }
                }

                return RedirectToAction(nameof(OrderConfirmation), new { id = order.Id });
            }

            ViewBag.Cart = cart; // Pass cart data again
            return View(order);
        }

        // POST: Order/ApplyVoucher
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyVoucher(string voucherCode)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var cart = await _context.LoadCartWithAvailableItemsAsync(userId);

            if (cart == null || !cart.CartItems.Any())
            {
                return Json(new { success = false, error = "Giỏ hàng của bạn đang trống." });
            }

            var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code.ToUpper() == voucherCode.ToUpper());
            var now = DateTime.Now;

            // --- Validation ---
            if (voucher == null) return Json(new { success = false, error = "Mã voucher không hợp lệ." });
            if (voucher.StartTime > now || (voucher.EndTime.HasValue && voucher.EndTime < now && !voucher.IsLifeTime)) return Json(new { success = false, error = "Voucher đã hết hạn hoặc chưa có hiệu lực." });
            if (voucher.Quantity <= voucher.Used) return Json(new { success = false, error = "Voucher đã hết lượt sử dụng." });
            if (voucher.Type == VoucherType.Private && voucher.UserId != userId) return Json(new { success = false, error = "Bạn không thể sử dụng voucher này." });

            var subtotal = cart.CartItems.Sum(item => GetCartItemUnitPrice(item) * item.Quantity);
            if (subtotal < voucher.MinimumRequirements) return Json(new { success = false, error = $"Đơn hàng tối thiểu phải là {voucher.MinimumRequirements:N0}đ." });

            // --- Calculate Discount ---
            double discountAmount = 0;
            if (voucher.DiscountType == VoucherDiscountType.Money)
            {
                discountAmount = voucher.Discount;
            }
            else // Percent
            {
                discountAmount = subtotal * (voucher.Discount / 100);
                if (!voucher.UnlimitedPercentageDiscount && voucher.MaximumPercentageReduction.HasValue && discountAmount > voucher.MaximumPercentageReduction.Value)
                {
                    discountAmount = voucher.MaximumPercentageReduction.Value;
                }
            }

            discountAmount = Math.Min(discountAmount, subtotal);

            // --- Calculate new totals ---
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


        // GET: Order/History
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

        public async Task<IActionResult> PayOsReturn(long id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null)
            {
                return NotFound();
            }

            if (order.PaymentType != PaymentType.PayNow || order.PaymentMethod != PaymentMethodType.Bank)
            {
                return RedirectToAction(nameof(OrderConfirmation), new { id = order.Id });
            }

            if (!_payOsService.ValidateRedirectSignature(Request.Query))
            {
                TempData["Error"] = "Không thể xác thực kết quả thanh toán PayOS.";
                return RedirectToAction(nameof(OrderConfirmation), new { id = order.Id });
            }

            var status = Request.Query["status"].ToString();
            if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "PAID", StringComparison.OrdinalIgnoreCase) && !string.Equals(status, "SUCCESS", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Thanh toán PayOS chưa được xác nhận.";
                return RedirectToAction(nameof(OrderConfirmation), new { id = order.Id });
            }

            if (order.Status != OrderStatus.Paid)
            {
                order.Status = OrderStatus.Paid;
                order.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(OrderConfirmation), new { id = order.Id });
        }

        public async Task<IActionResult> PayOsCancel(long id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null)
            {
                return NotFound();
            }

            if (order.PaymentType != PaymentType.PayNow || order.PaymentMethod != PaymentMethodType.Bank)
            {
                return RedirectToAction(nameof(OrderConfirmation), new { id = order.Id });
            }

            if (!_payOsService.ValidateRedirectSignature(Request.Query))
            {
                TempData["Error"] = "Không thể xác thực yêu cầu hủy thanh toán PayOS.";
                return RedirectToAction(nameof(OrderConfirmation), new { id = order.Id });
            }

            if (order.Status != OrderStatus.Paid)
            {
                order.Status = OrderStatus.Cancelled;
                order.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
            }

            TempData["Error"] = "Thanh toán PayOS đã bị hủy.";
            return RedirectToAction(nameof(OrderConfirmation), new { id = order.Id });
        }

        // GET: Order/OrderConfirmation
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
    }
}

