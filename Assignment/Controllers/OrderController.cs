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
using Assignment.Services.Payments;
using Microsoft.Extensions.Logging;
using Assignment.Options;
using Assignment.ViewModels;

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

            PrepareCheckoutViewData(cart, filteredItems, Enumerable.Empty<VoucherDiscountSummary>());
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
            var orderItems = filteredItems.Select(cartItem => new OrderItem
            {
                Price = GetCartItemUnitPrice(cartItem),
                Quantity = cartItem.Quantity,
                ProductId = cartItem.ProductId,
                ComboId = cartItem.ComboId,
                Product = cartItem.Product,
                Combo = cartItem.Combo
            }).ToList();

            order.OrderItems = orderItems;
            order.TotalQuantity = orderItems.Sum(oi => oi.Quantity);
            order.TotalPrice = orderItems.Sum(oi => oi.Price * oi.Quantity);

            var subtotal = order.TotalPrice;
            var appliedVoucherIds = order.AppliedVoucherIds?
                .Where(id => id > 0)
                .Distinct()
                .ToList() ?? new List<long>();

            var voucherSummaries = new List<VoucherDiscountSummary>();
            var appliedVouchers = new List<Voucher>();

            if (appliedVoucherIds.Any())
            {
                appliedVouchers = await _context.Vouchers
                    .Include(v => v.VoucherUsers)
                    .Include(v => v.VoucherProducts)
                    .Where(v => appliedVoucherIds.Contains(v.Id) && !v.IsDeleted)
                    .ToListAsync();

                appliedVouchers = appliedVouchers
                    .OrderBy(v => appliedVoucherIds.IndexOf(v.Id))
                    .ToList();

                if (appliedVouchers.Count != appliedVoucherIds.Count)
                {
                    ModelState.AddModelError(string.Empty, "Có voucher không hợp lệ hoặc đã bị xóa. Vui lòng kiểm tra lại.");
                }
                else
                {
                    var limitError = ValidateCombinedVoucherLimits(appliedVouchers);
                    if (limitError != null)
                    {
                        ModelState.AddModelError(string.Empty, limitError);
                    }
                    else
                    {
                        var calculationResult = TryCalculateVoucherDiscounts(appliedVouchers, filteredItems, subtotal, userId);
                        if (!calculationResult.Success)
                        {
                            ModelState.AddModelError(string.Empty, calculationResult.ErrorMessage ?? "Không thể áp dụng voucher.");
                        }
                        else
                        {
                            voucherSummaries = calculationResult.Summaries;
                        }
                    }
                }
            }

            order.Discount = Math.Min(voucherSummaries.Sum(summary => summary.DiscountAmount), subtotal);
            var previewPriceAfterDiscount = subtotal - order.Discount;
            order.Vat = previewPriceAfterDiscount * 0.15;
            order.TotalBill = previewPriceAfterDiscount + order.Vat;
            order.AppliedVoucherIds = appliedVouchers.Select(v => v.Id).ToList();

            if (!ModelState.IsValid)
            {
                PrepareCheckoutViewData(cart, filteredItems, voucherSummaries);
                return View(order);
            }

            order.UserId = userId;
            order.CreatedAt = DateTime.Now;
            order.Status = OrderStatus.Pending;
            order.VoucherId = voucherSummaries.Any() ? voucherSummaries.First().Id : (long?)null;
            order.OrderVouchers = voucherSummaries
                .Select(summary => new OrderVoucher
                {
                    VoucherId = summary.Id,
                    DiscountAmount = summary.DiscountAmount
                })
                .ToList();

            foreach (var voucher in appliedVouchers)
            {
                voucher.Used += 1;
                _context.Vouchers.Update(voucher);
            }

            var priceAfterDiscount = subtotal - order.Discount;
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyVoucher(string voucherCode, string? selectedCartItemIds, List<long>? appliedVoucherIds)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var cart = await _context.LoadCartWithAvailableItemsAsync(userId);

            if (cart == null || !cart.CartItems.Any())
            {
                return Json(new { success = false, error = "Giỏ hàng của bạn đang trống." });
            }

            if (string.IsNullOrWhiteSpace(voucherCode))
            {
                return Json(new { success = false, error = "Mã voucher không hợp lệ." });
            }

            var sanitizedVoucherIds = appliedVoucherIds?
                .Where(id => id > 0)
                .Distinct()
                .ToList() ?? new List<long>();

            var selectedIds = ParseSelectedCartItemIds(selectedCartItemIds);
            var filteredItems = FilterCartItems(cart.CartItems, selectedIds).ToList();

            if (!filteredItems.Any())
            {
                return Json(new { success = false, error = "Các sản phẩm đã chọn không còn khả dụng." });
            }

            var subtotal = filteredItems.Sum(item => GetCartItemUnitPrice(item) * item.Quantity);

            var existingVouchers = await _context.Vouchers
                .Include(v => v.VoucherUsers)
                .Include(v => v.VoucherProducts)
                .Where(v => sanitizedVoucherIds.Contains(v.Id) && !v.IsDeleted)
                .ToListAsync();

            existingVouchers = existingVouchers
                .OrderBy(v => sanitizedVoucherIds.IndexOf(v.Id))
                .ToList();

            if (existingVouchers.Count != sanitizedVoucherIds.Count)
            {
                return Json(new { success = false, error = "Một hoặc nhiều voucher đã không còn hợp lệ." });
            }

            if (existingVouchers.Any(v => v.Code.Equals(voucherCode, StringComparison.OrdinalIgnoreCase)))
            {
                return Json(new { success = false, error = "Voucher đã được áp dụng." });
            }

            var voucher = await _context.Vouchers
                .Include(v => v.VoucherUsers)
                .Include(v => v.VoucherProducts)
                .FirstOrDefaultAsync(v => v.Code.ToUpper() == voucherCode.ToUpper() && !v.IsDeleted);

            if (voucher == null)
            {
                return Json(new { success = false, error = "Mã voucher không hợp lệ." });
            }

            var combinedVouchers = new List<Voucher>(existingVouchers) { voucher };
            var limitError = ValidateCombinedVoucherLimits(combinedVouchers);
            if (limitError != null)
            {
                return Json(new { success = false, error = limitError });
            }

            var calculationResult = TryCalculateVoucherDiscounts(combinedVouchers, filteredItems, subtotal, userId);
            if (!calculationResult.Success)
            {
                return Json(new { success = false, error = calculationResult.ErrorMessage ?? "Không thể áp dụng voucher." });
            }

            var totalDiscount = Math.Min(calculationResult.TotalDiscount, subtotal);
            var priceAfterDiscount = Math.Max(0, subtotal - totalDiscount);
            var vatAmount = priceAfterDiscount * 0.15;
            var totalBill = priceAfterDiscount + vatAmount;

            return Json(new
            {
                success = true,
                vouchers = calculationResult.Summaries.Select(summary => new
                {
                    id = summary.Id,
                    code = summary.Code,
                    name = summary.Name,
                    discountAmount = summary.DiscountAmount
                }),
                totalDiscount,
                vatAmount,
                totalBill
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecalculateVouchers(List<long>? appliedVoucherIds, string? selectedCartItemIds)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var cart = await _context.LoadCartWithAvailableItemsAsync(userId);

            if (cart == null || !cart.CartItems.Any())
            {
                return Json(new { success = false, error = "Giỏ hàng của bạn đang trống." });
            }

            var sanitizedVoucherIds = appliedVoucherIds?
                .Where(id => id > 0)
                .Distinct()
                .ToList() ?? new List<long>();

            var selectedIds = ParseSelectedCartItemIds(selectedCartItemIds);
            var filteredItems = FilterCartItems(cart.CartItems, selectedIds).ToList();

            if (!filteredItems.Any())
            {
                return Json(new { success = false, error = "Các sản phẩm đã chọn không còn khả dụng." });
            }

            var subtotal = filteredItems.Sum(item => GetCartItemUnitPrice(item) * item.Quantity);

            if (!sanitizedVoucherIds.Any())
            {
                var vatAmountWithoutVouchers = subtotal * 0.15;
                var totalBillWithoutVouchers = subtotal + vatAmountWithoutVouchers;
                return Json(new
                {
                    success = true,
                    vouchers = Array.Empty<object>(),
                    totalDiscount = 0,
                    vatAmount = vatAmountWithoutVouchers,
                    totalBill = totalBillWithoutVouchers
                });
            }

            var vouchers = await _context.Vouchers
                .Include(v => v.VoucherUsers)
                .Include(v => v.VoucherProducts)
                .Where(v => sanitizedVoucherIds.Contains(v.Id) && !v.IsDeleted)
                .ToListAsync();

            vouchers = vouchers
                .OrderBy(v => sanitizedVoucherIds.IndexOf(v.Id))
                .ToList();

            if (vouchers.Count != sanitizedVoucherIds.Count)
            {
                return Json(new { success = false, error = "Một hoặc nhiều voucher không còn hợp lệ." });
            }

            var limitError = ValidateCombinedVoucherLimits(vouchers);
            if (limitError != null)
            {
                return Json(new { success = false, error = limitError });
            }

            var calculationResult = TryCalculateVoucherDiscounts(vouchers, filteredItems, subtotal, userId);
            if (!calculationResult.Success)
            {
                return Json(new { success = false, error = calculationResult.ErrorMessage ?? "Không thể áp dụng voucher." });
            }

            var totalDiscount = Math.Min(calculationResult.TotalDiscount, subtotal);
            var priceAfterDiscount = Math.Max(0, subtotal - totalDiscount);
            var vatAmount = priceAfterDiscount * 0.15;
            var totalBill = priceAfterDiscount + vatAmount;

            return Json(new
            {
                success = true,
                vouchers = calculationResult.Summaries.Select(summary => new
                {
                    id = summary.Id,
                    code = summary.Code,
                    name = summary.Name,
                    discountAmount = summary.DiscountAmount
                }),
                totalDiscount,
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

        public async Task<IActionResult> Manage(OrderStatus? status, string? search, int page = 1, int pageSize = PaginationDefaults.DefaultPageSize)
        {
            if (!CanManageOrders())
            {
                return Forbid();
            }

            page = PaginationDefaults.NormalizePage(page);
            pageSize = PaginationDefaults.NormalizePageSize(pageSize);

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

            var totalItems = await ordersQuery.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            if (totalPages > 0 && page > totalPages)
            {
                page = totalPages;
            }

            var orders = await ordersQuery
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var viewModel = new PagedResult<Order>
            {
                CurrentPage = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                PageSizeOptions = PaginationDefaults.PageSizeOptions
            };

            viewModel.SetItems(orders);

            ViewBag.StatusFilter = status;
            ViewBag.SearchTerm = search;
            ViewBag.CanUpdateStatus = CanManageOrders();

            return View(viewModel.EnsureValidPage());
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
        public async Task<IActionResult> UpdateStatus(long id, OrderStatus status, OrderStatus? statusFilter, string? search, int page = 1, int pageSize = 25)
        {
            if (!CanManageOrders())
            {
                return Forbid();
            }

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted);
            if (order == null)
            {
                TempData["Error"] = "Không tìm thấy đơn hàng.";
                return RedirectToAction(nameof(Manage), new { status = statusFilter, search, page, pageSize });
            }

            if (order.Status == status)
            {
                TempData["Info"] = "Đơn hàng đã ở trạng thái này.";
                return RedirectToAction(nameof(Manage), new { status = statusFilter, search, page, pageSize });
            }

            order.Status = status;
            order.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Cập nhật trạng thái đơn hàng #{order.Id} thành công.";
            return RedirectToAction(nameof(Manage), new { status = statusFilter, search, page, pageSize });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkUpdateStatus([FromForm] List<long> selectedIds, OrderStatus status, OrderStatus? statusFilter, string? search, int page = 1, int pageSize = 25)
        {
            if (!CanManageOrders())
            {
                return Forbid();
            }

            if (selectedIds == null || selectedIds.Count == 0)
            {
                TempData["Info"] = "Vui lòng chọn ít nhất một đơn hàng để cập nhật.";
                return RedirectToAction(nameof(Manage), new { status = statusFilter, search, page, pageSize });
            }

            var orders = await _context.Orders
                .Where(o => selectedIds.Contains(o.Id) && !o.IsDeleted)
                .ToListAsync();

            if (!orders.Any())
            {
                TempData["Info"] = "Không tìm thấy đơn hàng hợp lệ để cập nhật.";
                return RedirectToAction(nameof(Manage), new { status = statusFilter, search, page, pageSize });
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

            return RedirectToAction(nameof(Manage), new { status = statusFilter, search, page, pageSize });
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

        private static string? ValidateCombinedVoucherLimits(IReadOnlyCollection<Voucher> vouchers)
        {
            if (vouchers == null)
            {
                return null;
            }

            var totalCount = vouchers.Count;
            foreach (var voucher in vouchers)
            {
                if (voucher.HasCombinedUsageLimit && voucher.MaxCombinedUsageCount.HasValue && totalCount > voucher.MaxCombinedUsageCount.Value)
                {
                    return $"Voucher {voucher.Code} chỉ cho phép áp dụng tối đa {voucher.MaxCombinedUsageCount.Value} voucher.";
                }
            }

            return null;
        }

        private (bool Success, string? ErrorMessage, double DiscountAmount) TryCalculateVoucherDiscount(
            Voucher voucher,
            IReadOnlyCollection<CartItem> filteredItems,
            double subtotal,
            string? userId)
        {
            if (voucher == null)
            {
                return (false, "Voucher không hợp lệ.", 0);
            }

            var now = DateTime.Now;
            if (voucher.StartTime > now || (voucher.EndTime.HasValue && voucher.EndTime.Value < now && !voucher.IsLifeTime))
            {
                return (false, "Voucher đã hết hạn hoặc chưa có hiệu lực.", 0);
            }

            if (voucher.Quantity <= voucher.Used)
            {
                return (false, "Voucher đã hết lượt sử dụng.", 0);
            }

            var allowedUserIds = voucher.VoucherUsers?
                .Where(vu => !vu.IsDeleted)
                .Select(vu => vu.UserId)
                .ToHashSet() ?? new HashSet<string>();

            if (!string.IsNullOrWhiteSpace(voucher.UserId))
            {
                allowedUserIds.Add(voucher.UserId);
            }

            if (voucher.Type == VoucherType.Private && (userId == null || !allowedUserIds.Contains(userId)))
            {
                return (false, "Bạn không thể sử dụng voucher này.", 0);
            }

            if (subtotal < voucher.MinimumRequirements)
            {
                return (false, $"Đơn hàng tối thiểu phải là {voucher.MinimumRequirements:N0}đ.", 0);
            }

            var discountBase = subtotal;

            if (voucher.ProductScope == VoucherProductScope.SelectedProducts)
            {
                var allowedProductIds = voucher.VoucherProducts?
                    .Where(vp => !vp.IsDeleted)
                    .Select(vp => vp.ProductId)
                    .ToHashSet() ?? new HashSet<long>();

                discountBase = filteredItems
                    .Where(item => item.Product != null && allowedProductIds.Contains(item.Product.Id))
                    .Sum(item => GetCartItemUnitPrice(item) * item.Quantity);

                if (discountBase <= 0)
                {
                    return (false, "Voucher không áp dụng cho sản phẩm đã chọn.", 0);
                }
            }

            double discountAmount;
            if (voucher.DiscountType == VoucherDiscountType.Money)
            {
                discountAmount = voucher.Discount;
            }
            else
            {
                discountAmount = discountBase * (voucher.Discount / 100);
                if (!voucher.UnlimitedPercentageDiscount && voucher.MaximumPercentageReduction.HasValue && discountAmount > voucher.MaximumPercentageReduction.Value)
                {
                    discountAmount = voucher.MaximumPercentageReduction.Value;
                }
            }

            discountAmount = Math.Min(discountAmount, discountBase);
            return (true, null, discountAmount);
        }

        private (bool Success, string? ErrorMessage, List<VoucherDiscountSummary> Summaries, double TotalDiscount) TryCalculateVoucherDiscounts(
            IReadOnlyCollection<Voucher> vouchers,
            IReadOnlyCollection<CartItem> filteredItems,
            double subtotal,
            string? userId)
        {
            var summaries = new List<VoucherDiscountSummary>();
            double totalDiscount = 0;

            if (vouchers == null || vouchers.Count == 0)
            {
                return (true, null, summaries, 0);
            }

            foreach (var voucher in vouchers)
            {
                var result = TryCalculateVoucherDiscount(voucher, filteredItems, subtotal, userId);
                if (!result.Success)
                {
                    return (false, BuildVoucherErrorMessage(voucher, result.ErrorMessage), new List<VoucherDiscountSummary>(), 0);
                }

                totalDiscount += result.DiscountAmount;
                summaries.Add(new VoucherDiscountSummary(voucher.Id, voucher.Code, voucher.Name, result.DiscountAmount));
            }

            return (true, null, summaries, totalDiscount);
        }

        private void PrepareCheckoutViewData(Cart cart, List<CartItem> filteredItems, IEnumerable<VoucherDiscountSummary> summaries)
        {
            cart.CartItems = filteredItems;
            ViewBag.Cart = cart;

            var subtotal = filteredItems.Sum(item => GetCartItemUnitPrice(item) * item.Quantity);
            SetInitialVoucherViewData(summaries ?? Enumerable.Empty<VoucherDiscountSummary>(), subtotal);
        }

        private void SetInitialVoucherViewData(IEnumerable<VoucherDiscountSummary> summaries, double subtotal)
        {
            var summaryList = summaries?
                .Select(summary => new
                {
                    summary.Id,
                    summary.Code,
                    summary.Name,
                    summary.DiscountAmount
                })
                .Cast<object>()
                .ToList() ?? new List<object>();

            var totalDiscount = summaries?.Sum(summary => summary.DiscountAmount) ?? 0;
            totalDiscount = Math.Min(totalDiscount, subtotal);
            var priceAfterDiscount = Math.Max(0, subtotal - totalDiscount);
            var vatAmount = priceAfterDiscount * 0.15;
            var totalBill = priceAfterDiscount + vatAmount;

            ViewBag.InitialAppliedVouchers = summaryList;
            ViewBag.InitialVoucherTotals = new
            {
                totalDiscount,
                vatAmount,
                totalBill
            };
        }

        private static string BuildVoucherErrorMessage(Voucher voucher, string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return $"Voucher {voucher?.Code ?? string.Empty} không hợp lệ.";
            }

            return voucher == null
                ? message
                : $"Voucher {voucher.Code}: {message}";
        }

        private record VoucherDiscountSummary(long Id, string Code, string Name, double DiscountAmount);

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

