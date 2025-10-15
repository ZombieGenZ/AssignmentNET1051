using Assignment.Data;
using Assignment.Enums;
using Assignment.Models;
using Assignment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Assignment.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public OrderController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
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
            var applicationUser = user as ApplicationUser;

            var order = new Order
            {
                Name = applicationUser?.FullName ?? user?.UserName ?? User.Identity?.Name ?? string.Empty,
                Email = user?.Email,
                Phone = user?.PhoneNumber ?? string.Empty,
                UserId = userId,
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

            long? voucherId = null;
            if (!string.IsNullOrEmpty(order.VoucherId) && long.TryParse(order.VoucherId, out var parsedId))
            {
                voucherId = parsedId;
            }

            if (order.PaymentType == PaymentType.PayNow)
            {
                if (order.PaymentMethod != PaymentMethodType.Bank)
                {
                    order.PaymentMethod = PaymentMethodType.Bank;
                }
            }
            else
            {
                order.PaymentMethod = null;
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
                    orderItems.Add(new OrderItem
                    {
                        Price = cartItem.Product?.Price ?? cartItem.Combo?.Price ?? 0,
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

                return RedirectToAction("OrderConfirmation", new { id = order.Id });
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

            var subtotal = cart.CartItems.Sum(item => (item.Product?.Price ?? item.Combo?.Price ?? 0) * item.Quantity);
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
    }
}

