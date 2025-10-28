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
using Assignment.ViewModels.Vouchers;

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

        public async Task<IActionResult> Checkout(string? cartItemIds, string? selectedSelectionIds)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var cart = await _context.LoadCartWithAvailableItemsAsync(userId);

            if (cart == null || !cart.CartItems.Any())
            {
                TempData["Error"] = "Giỏ hàng của bạn đang trống. Vui lòng thêm sản phẩm trước khi thanh toán.";
                return RedirectToAction("Index", "Cart");
            }

            var selectedIds = ParseSelectedCartItemIds(cartItemIds);
            var selectedSelectionSet = ParseSelectedCartSelectionIds(selectedSelectionIds);
            var filteredItems = FilterCartItems(cart.CartItems, selectedIds, selectedSelectionSet);

            if (!filteredItems.Any())
            {
                TempData["Error"] = "Các sản phẩm đã chọn không còn khả dụng.";
                return RedirectToAction("Index", "Cart");
            }

            var user = await _userManager.GetUserAsync(User);
            var sanitizedSelectionIds = filteredItems
                .SelectMany(ci => ci.ProductTypeSelections ?? Enumerable.Empty<CartItemProductType>())
                .Select(selection => selection.Id)
                .Distinct()
                .ToList();
            var order = new Order
            {
                Name = !string.IsNullOrWhiteSpace(user?.FullName)
                    ? user.FullName
                    : User.Identity?.Name ?? string.Empty,
                Email = user?.Email,
                Phone = user?.PhoneNumber ?? string.Empty,
                UserId = userId,
                SelectedCartItemIds = string.Join(',', filteredItems.Select(ci => ci.Id)),
                SelectedCartSelectionIds = string.Join(',', sanitizedSelectionIds)
            };

            await PrepareCheckoutViewData(cart, filteredItems, Enumerable.Empty<VoucherDiscountSummary>());
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(Order order)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var cart = await _context.LoadCartWithAvailableItemsAsync(userId);
            var user = await _userManager.GetUserAsync(User);

            if (cart == null || !cart.CartItems.Any())
            {
                ModelState.AddModelError("", "Giỏ hàng của bạn trống.");
                ViewBag.Cart = cart;
                return View(order);
            }

            var selectedIds = ParseSelectedCartItemIds(order.SelectedCartItemIds);
            var selectedSelectionSet = ParseSelectedCartSelectionIds(order.SelectedCartSelectionIds);
            var filteredItems = FilterCartItems(cart.CartItems, selectedIds, selectedSelectionSet);

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
            order.SelectedCartSelectionIds = string.Join(',', filteredItems
                .SelectMany(ci => ci.ProductTypeSelections ?? Enumerable.Empty<CartItemProductType>())
                .Select(selection => selection.Id)
                .Distinct());
            var orderItems = new List<OrderItem>();

            foreach (var cartItem in filteredItems)
            {
                if (cartItem.Product != null && cartItem.ProductTypeSelections != null && cartItem.ProductTypeSelections.Any())
                {
                    var selectionItems = cartItem.ProductTypeSelections
                        .Select(selection => new OrderItemProductType
                        {
                            ProductTypeId = selection.ProductTypeId,
                            Quantity = selection.Quantity,
                            UnitPrice = selection.UnitPrice
                        })
                        .ToList();

                    var totalSelectionQuantity = selectionItems.Sum(selection => (long)selection.Quantity);

                    orderItems.Add(new OrderItem
                    {
                        ProductId = cartItem.ProductId,
                        Product = cartItem.Product,
                        Quantity = totalSelectionQuantity,
                        Price = 0,
                        ProductTypeSelections = selectionItems
                    });
                }
                else
                {
                    orderItems.Add(new OrderItem
                    {
                        Price = GetCartItemUnitPrice(cartItem),
                        Quantity = cartItem.Quantity,
                        ProductId = cartItem.ProductId,
                        ComboId = cartItem.ComboId,
                        Product = cartItem.Product,
                        Combo = cartItem.Combo
                    });
                }
            }

            order.OrderItems = orderItems;
            order.TotalQuantity = orderItems.Sum(GetOrderItemTotalQuantity);
            order.TotalPrice = orderItems.Sum(GetOrderItemTotalPrice);

            var subtotal = order.TotalPrice;
            var isNewCustomer = await IsNewCustomerAsync(userId);
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
                    .Include(v => v.VoucherCombos)
                    .Where(v => appliedVoucherIds.Contains(v.Id) && !v.IsDeleted && v.IsPublish)
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
                        var calculationResult = TryCalculateVoucherDiscounts(appliedVouchers, filteredItems, subtotal, userId, isNewCustomer, user);
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
                await PrepareCheckoutViewData(cart, filteredItems, voucherSummaries);
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

            var cartItemIdsToRemove = filteredItems
                .Select(item => item.Id)
                .Distinct()
                .ToList();

            if (cartItemIdsToRemove.Count > 0)
            {
                var trackedItemsToRemove = cart.CartItems?
                    .Where(ci => cartItemIdsToRemove.Contains(ci.Id))
                    .ToList() ?? new List<CartItem>();

                if (trackedItemsToRemove.Count < cartItemIdsToRemove.Count)
                {
                    var missingIds = cartItemIdsToRemove
                        .Except(trackedItemsToRemove.Select(ci => ci.Id))
                        .ToList();

                    if (missingIds.Count > 0)
                    {
                        var additionalItems = await _context.CartItems
                            .Where(ci => missingIds.Contains(ci.Id))
                            .ToListAsync();

                        trackedItemsToRemove.AddRange(additionalItems);
                    }
                }

                if (trackedItemsToRemove.Count > 0)
                {
                    _context.CartItems.RemoveRange(trackedItemsToRemove);
                }
            }

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
        public async Task<IActionResult> ApplyVoucher(string voucherCode, string? selectedCartItemIds, string? selectedCartSelectionIds, List<long>? appliedVoucherIds)
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
            var selectedSelectionSet = ParseSelectedCartSelectionIds(selectedCartSelectionIds);
            var filteredItems = FilterCartItems(cart.CartItems, selectedIds, selectedSelectionSet).ToList();

            if (!filteredItems.Any())
            {
                return Json(new { success = false, error = "Các sản phẩm đã chọn không còn khả dụng." });
            }

            var subtotal = filteredItems.Sum(GetCartItemTotalPrice);
            var isNewCustomer = await IsNewCustomerAsync(userId);
            var user = string.IsNullOrWhiteSpace(userId)
                ? null
                : await _userManager.FindByIdAsync(userId);

            var existingVouchers = await _context.Vouchers
                .Include(v => v.VoucherUsers)
                .Include(v => v.VoucherProducts)
                .Include(v => v.VoucherCombos)
                .Where(v => sanitizedVoucherIds.Contains(v.Id) && !v.IsDeleted && v.IsPublish)
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
                .Include(v => v.VoucherCombos)
                .FirstOrDefaultAsync(v => v.Code.ToUpper() == voucherCode.ToUpper() && !v.IsDeleted && v.IsPublish);

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

            var calculationResult = TryCalculateVoucherDiscounts(combinedVouchers, filteredItems, subtotal, userId, isNewCustomer, user);
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
        public async Task<IActionResult> RecalculateVouchers(List<long>? appliedVoucherIds, string? selectedCartItemIds, string? selectedCartSelectionIds)
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
            var selectedSelectionSet = ParseSelectedCartSelectionIds(selectedCartSelectionIds);
            var filteredItems = FilterCartItems(cart.CartItems, selectedIds, selectedSelectionSet).ToList();

            if (!filteredItems.Any())
            {
                return Json(new { success = false, error = "Các sản phẩm đã chọn không còn khả dụng." });
            }

            var subtotal = filteredItems.Sum(GetCartItemTotalPrice);
            var isNewCustomer = await IsNewCustomerAsync(userId);
            var user = string.IsNullOrWhiteSpace(userId)
                ? null
                : await _userManager.FindByIdAsync(userId);

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
                .Include(v => v.VoucherCombos)
                .Where(v => sanitizedVoucherIds.Contains(v.Id) && !v.IsDeleted && v.IsPublish)
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

            var calculationResult = TryCalculateVoucherDiscounts(vouchers, filteredItems, subtotal, userId, isNewCustomer, user);
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
        public async Task<IActionResult> GetApplicableVouchers(string? selectedCartItemIds, string? selectedCartSelectionIds, List<long>? appliedVoucherIds)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var cart = await _context.LoadCartWithAvailableItemsAsync(userId);

            if (cart == null || !cart.CartItems.Any())
            {
                return Json(new { success = false, error = "Giỏ hàng của bạn đang trống." });
            }

            var selectedIds = ParseSelectedCartItemIds(selectedCartItemIds);
            var selectedSelectionSet = ParseSelectedCartSelectionIds(selectedCartSelectionIds);
            var filteredItems = FilterCartItems(cart.CartItems, selectedIds, selectedSelectionSet);

            if (!filteredItems.Any())
            {
                return Json(new { success = false, error = "Các sản phẩm đã chọn không còn khả dụng." });
            }

            var subtotal = filteredItems.Sum(GetCartItemTotalPrice);
            var sanitizedAppliedIds = appliedVoucherIds?
                .Where(id => id > 0)
                .Distinct()
                .ToList() ?? new List<long>();

            var voucherOptions = await BuildCheckoutVoucherOptionsAsync(filteredItems, subtotal, sanitizedAppliedIds, userId);
            var projectedOptions = ProjectVoucherOptions(voucherOptions);

            return Json(new
            {
                success = true,
                privateVouchers = projectedOptions.Private,
                savedVouchers = projectedOptions.Saved
            });
        }


        public async Task<IActionResult> History()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var orders = await _context.Orders
                .Where(o => o.UserId == userId)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.ProductTypes)
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
                    .ThenInclude(oi => oi.Product)!
                        .ThenInclude(p => p.ProductTypes)
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
            ViewBag.CanUpdateStatus = CanUpdateOrderStatus();

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
                    .ThenInclude(oi => oi.Product)!
                        .ThenInclude(p => p.ProductTypes)
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
            if (!CanManageOrders() || !CanUpdateOrderStatus())
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

            if (status == OrderStatus.Completed)
            {
                await ApplyLoyaltyRewardsAsync(order);
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Cập nhật trạng thái đơn hàng #{order.Id} thành công.";
            return RedirectToAction(nameof(Manage), new { status = statusFilter, search, page, pageSize });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkUpdateStatus([FromForm] List<long> selectedIds, OrderStatus status, OrderStatus? statusFilter, string? search, int page = 1, int pageSize = 25)
        {
            if (!CanManageOrders() || !CanUpdateOrderStatus())
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

                if (status == OrderStatus.Completed)
                {
                    await ApplyLoyaltyRewardsAsync(order);
                }
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

        private async Task ApplyLoyaltyRewardsAsync(Order order)
        {
            if (order == null || order.LoyaltyRewardsApplied || string.IsNullOrEmpty(order.UserId))
            {
                return;
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == order.UserId);
            if (user == null)
            {
                order.LoyaltyRewardsApplied = true;
                return;
            }

            var baseValue = (long)Math.Round(order.TotalBill, MidpointRounding.AwayFromZero);
            if (baseValue <= 0)
            {
                order.LoyaltyRewardsApplied = true;
                return;
            }

            var booster = user.Booster <= 0 ? 1m : user.Booster;
            var expEarned = (long)Math.Floor(baseValue * 2m * booster);
            var pointEarned = (long)Math.Floor(baseValue * booster);

            user.Exp += expEarned;
            user.Point += pointEarned;
            user.TotalPoint += pointEarned;
            user.Rank = CustomerRankCalculator.CalculateRank(user.Exp);

            if (user.Rank == CustomerRank.Emerald && user.Booster < 2m)
            {
                user.Booster = 2m;
            }
            else if (user.Booster <= 0)
            {
                user.Booster = 1m;
            }

            order.LoyaltyRewardsApplied = true;

            _context.Users.Update(user);
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
                        .ThenInclude(p => p.ProductTypes)
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

        private static ISet<long> ParseSelectedCartSelectionIds(string? selectedCartSelectionIds)
        {
            var result = new HashSet<long>();

            if (string.IsNullOrWhiteSpace(selectedCartSelectionIds))
            {
                return result;
            }

            var parts = selectedCartSelectionIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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
            string? userId,
            bool isNewCustomer,
            ApplicationUser? user)
        {
            if (voucher == null)
            {
                return (false, "Voucher không hợp lệ.", 0);
            }

            if (!voucher.IsPublish)
            {
                return (false, "Voucher hiện không khả dụng.", 0);
            }

            if (voucher.IsForNewUsersOnly)
            {
                if (string.IsNullOrWhiteSpace(userId) || !isNewCustomer)
                {
                    return (false, "Voucher chỉ áp dụng cho khách hàng mới.", 0);
                }
            }

            if (voucher.MinimumRank.HasValue)
            {
                if (user == null)
                {
                    return (false, "Vui lòng đăng nhập để sử dụng voucher này.", 0);
                }

                if (user.Rank < voucher.MinimumRank.Value)
                {
                    return (false, $"Voucher yêu cầu khách hàng đạt cấp {voucher.MinimumRank.Value.GetDisplayName()} trở lên.", 0);
                }
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

            var productItems = filteredItems.Where(item => item.Product != null).ToList();
            var comboItems = filteredItems.Where(item => item.Combo != null).ToList();

            double productBase;
            if (voucher.ProductScope == VoucherProductScope.SelectedProducts)
            {
                var allowedProductIds = voucher.VoucherProducts?
                    .Where(vp => !vp.IsDeleted)
                    .Select(vp => vp.ProductId)
                    .ToHashSet() ?? new HashSet<long>();

                productBase = productItems
                    .Where(item => item.Product != null && allowedProductIds.Contains(item.Product.Id))
                    .Sum(item => GetCartItemTotalPrice(item));
            }
            else if (voucher.ProductScope == VoucherProductScope.NoProducts)
            {
                productBase = 0;
            }
            else
            {
                productBase = productItems
                    .Sum(item => GetCartItemTotalPrice(item));
            }

            double comboBase;
            if (voucher.ComboScope == VoucherComboScope.SelectedCombos)
            {
                var allowedComboIds = voucher.VoucherCombos?
                    .Where(vc => !vc.IsDeleted)
                    .Select(vc => vc.ComboId)
                    .ToHashSet() ?? new HashSet<long>();

                comboBase = comboItems
                    .Where(item => item.Combo != null && allowedComboIds.Contains(item.Combo.Id))
                    .Sum(item => GetCartItemTotalPrice(item));
            }
            else if (voucher.ComboScope == VoucherComboScope.NoCombos)
            {
                comboBase = 0;
            }
            else
            {
                comboBase = comboItems
                    .Sum(item => GetCartItemTotalPrice(item));
            }

            var discountBase = productBase + comboBase;

            if (discountBase <= 0)
            {
                var hasScopeRestriction = voucher.ProductScope == VoucherProductScope.SelectedProducts
                                           || voucher.ProductScope == VoucherProductScope.NoProducts
                                           || voucher.ComboScope == VoucherComboScope.SelectedCombos
                                           || voucher.ComboScope == VoucherComboScope.NoCombos;

                var scopeMessage = hasScopeRestriction
                    ? "Voucher không áp dụng cho sản phẩm hoặc combo đã chọn."
                    : "Voucher không áp dụng cho giỏ hàng hiện tại.";

                return (false, scopeMessage, 0);
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
            string? userId,
            bool isNewCustomer,
            ApplicationUser? user)
        {
            var summaries = new List<VoucherDiscountSummary>();
            double totalDiscount = 0;

            if (vouchers == null || vouchers.Count == 0)
            {
                return (true, null, summaries, 0);
            }

            foreach (var voucher in vouchers)
            {
                var result = TryCalculateVoucherDiscount(voucher, filteredItems, subtotal, userId, isNewCustomer, user);
                if (!result.Success)
                {
                    return (false, BuildVoucherErrorMessage(voucher, result.ErrorMessage), new List<VoucherDiscountSummary>(), 0);
                }

                totalDiscount += result.DiscountAmount;
                summaries.Add(new VoucherDiscountSummary(voucher.Id, voucher.Code, voucher.Name, result.DiscountAmount));
            }

            return (true, null, summaries, totalDiscount);
        }

        private async Task PrepareCheckoutViewData(Cart cart, List<CartItem> filteredItems, IEnumerable<VoucherDiscountSummary> summaries)
        {
            cart.CartItems = filteredItems;
            ViewBag.Cart = cart;

            var subtotal = filteredItems.Sum(GetCartItemTotalPrice);
            SetInitialVoucherViewData(summaries ?? Enumerable.Empty<VoucherDiscountSummary>(), subtotal);

            var appliedIds = summaries?.Select(summary => summary.Id).ToList() ?? new List<long>();
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var voucherOptions = await BuildCheckoutVoucherOptionsAsync(filteredItems, subtotal, appliedIds, userId);
            var projectedVoucherOptions = ProjectVoucherOptions(voucherOptions);
            ViewBag.InitialVoucherOptions = new
            {
                privateVouchers = projectedVoucherOptions.Private,
                savedVouchers = projectedVoucherOptions.Saved
            };
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

        private async Task<CheckoutVoucherOptionsViewModel> BuildCheckoutVoucherOptionsAsync(
            IReadOnlyCollection<CartItem> filteredItems,
            double subtotal,
            IReadOnlyCollection<long> appliedVoucherIds,
            string? userId)
        {
            if (filteredItems == null || filteredItems.Count == 0 || subtotal <= 0 || string.IsNullOrWhiteSpace(userId))
            {
                return new CheckoutVoucherOptionsViewModel();
            }

            var user = await _userManager.FindByIdAsync(userId);
            var sanitizedAppliedIds = appliedVoucherIds?
                .Where(id => id > 0)
                .Distinct()
                .ToList() ?? new List<long>();

            var appliedVouchers = sanitizedAppliedIds.Any()
                ? await _context.Vouchers
                    .AsNoTracking()
                    .Include(v => v.VoucherUsers)
                    .Include(v => v.VoucherProducts)
                    .Include(v => v.VoucherCombos)
                    .Where(v => sanitizedAppliedIds.Contains(v.Id) && !v.IsDeleted && v.IsPublish)
                    .ToListAsync()
                : new List<Voucher>();

            var orderedAppliedVouchers = appliedVouchers
                .OrderBy(v => sanitizedAppliedIds.IndexOf(v.Id))
                .ToList();

            var isNewCustomer = await IsNewCustomerAsync(userId);

            var privateVouchers = await _context.Vouchers
                .AsNoTracking()
                .Include(v => v.VoucherUsers)
                .Include(v => v.VoucherProducts)
                .Include(v => v.VoucherCombos)
                .Where(v => !v.IsDeleted && v.IsPublish && v.Type == VoucherType.Private &&
                            userId != null &&
                            (v.UserId == userId ||
                             v.VoucherUsers.Any(vu => !vu.IsDeleted && !vu.IsSaved && vu.UserId == userId)))
                .ToListAsync();

            var savedVouchers = await _context.Vouchers
                .AsNoTracking()
                .Include(v => v.VoucherUsers)
                .Include(v => v.VoucherProducts)
                .Include(v => v.VoucherCombos)
                .Where(v => !v.IsDeleted && v.IsPublish &&
                            userId != null &&
                            v.VoucherUsers.Any(vu => !vu.IsDeleted && vu.IsSaved && vu.UserId == userId))
                .ToListAsync();

            var privateOptions = BuildVoucherOptionsList(
                privateVouchers,
                orderedAppliedVouchers,
                sanitizedAppliedIds,
                filteredItems,
                subtotal,
                userId,
                false,
                isNewCustomer,
                user);

            var savedOptions = BuildVoucherOptionsList(
                savedVouchers,
                orderedAppliedVouchers,
                sanitizedAppliedIds,
                filteredItems,
                subtotal,
                userId,
                true,
                isNewCustomer,
                user);

            return new CheckoutVoucherOptionsViewModel
            {
                PrivateVouchers = privateOptions,
                SavedVouchers = savedOptions
            };
        }

        private List<CheckoutVoucherOptionViewModel> BuildVoucherOptionsList(
            IEnumerable<Voucher>? candidates,
            IReadOnlyCollection<Voucher> appliedVouchers,
            IReadOnlyCollection<long> appliedVoucherIds,
            IReadOnlyCollection<CartItem> filteredItems,
            double subtotal,
            string? userId,
            bool isSavedGroup,
            bool isNewCustomer,
            ApplicationUser? user)
        {
            var options = new List<CheckoutVoucherOptionViewModel>();
            if (candidates == null)
            {
                return options;
            }

            var appliedIds = appliedVoucherIds?.ToHashSet() ?? new HashSet<long>();
            var appliedVoucherList = appliedVouchers?.ToList() ?? new List<Voucher>();

            foreach (var voucher in candidates)
            {
                if (voucher == null || appliedIds.Contains(voucher.Id))
                {
                    continue;
                }

                var combined = new List<Voucher>(appliedVoucherList) { voucher };
                var limitError = ValidateCombinedVoucherLimits(combined);
                if (limitError != null)
                {
                    continue;
                }

                var calculation = TryCalculateVoucherDiscount(voucher, filteredItems, subtotal, userId, isNewCustomer, user);
                if (!calculation.Success || calculation.DiscountAmount <= 0)
                {
                    continue;
                }

                options.Add(new CheckoutVoucherOptionViewModel
                {
                    Id = voucher.Id,
                    Code = voucher.Code,
                    Name = voucher.Name,
                    Description = voucher.Description,
                    Type = voucher.Type,
                    DiscountType = voucher.DiscountType,
                    Discount = voucher.Discount,
                    UnlimitedPercentageDiscount = voucher.UnlimitedPercentageDiscount,
                    MaximumPercentageReduction = voucher.MaximumPercentageReduction,
                    MinimumRequirements = voucher.MinimumRequirements,
                    PotentialDiscount = calculation.DiscountAmount,
                    IsSaved = isSavedGroup,
                    IsLifeTime = voucher.IsLifeTime,
                    StartTime = voucher.StartTime,
                    EndTime = voucher.EndTime,
                    Quantity = voucher.Quantity,
                    Used = voucher.Used,
                    HasCombinedUsageLimit = voucher.HasCombinedUsageLimit,
                    MaxCombinedUsageCount = voucher.MaxCombinedUsageCount,
                    IsForNewUsersOnly = voucher.IsForNewUsersOnly,
                    Group = isSavedGroup ? "saved" : "private"
                });
            }

            return options
                .OrderByDescending(option => option.PotentialDiscount)
                .ThenBy(option => option.MinimumRequirements)
                .ThenBy(option => option.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static (List<object> Private, List<object> Saved) ProjectVoucherOptions(CheckoutVoucherOptionsViewModel options)
        {
            if (options == null)
            {
                return (new List<object>(), new List<object>());
            }

            var privateOptions = options.PrivateVouchers?
                .Select(ProjectVoucherOption)
                .Cast<object>()
                .ToList() ?? new List<object>();

            var savedOptions = options.SavedVouchers?
                .Select(ProjectVoucherOption)
                .Cast<object>()
                .ToList() ?? new List<object>();

            return (privateOptions, savedOptions);
        }

        private static object ProjectVoucherOption(CheckoutVoucherOptionViewModel option)
        {
            return new
            {
                id = option.Id,
                code = option.Code,
                name = option.Name,
                description = option.Description,
                type = option.Type,
                discountType = option.DiscountType,
                discount = option.Discount,
                unlimitedPercentageDiscount = option.UnlimitedPercentageDiscount,
                maximumPercentageReduction = option.MaximumPercentageReduction,
                minimumRequirements = option.MinimumRequirements,
                potentialDiscount = option.PotentialDiscount,
                isSaved = option.IsSaved,
                isLifeTime = option.IsLifeTime,
                startTime = option.StartTime,
                endTime = option.EndTime,
                quantity = option.Quantity,
                used = option.Used,
                hasCombinedUsageLimit = option.HasCombinedUsageLimit,
                maxCombinedUsageCount = option.MaxCombinedUsageCount,
                isForNewUsersOnly = option.IsForNewUsersOnly,
                group = option.Group
            };
        }

        private async Task<bool> IsNewCustomerAsync(string? userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            return !await _context.Orders
                .AsNoTracking()
                .AnyAsync(o => o.UserId == userId && !o.IsDeleted && o.Status != OrderStatus.Cancelled);
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

        private static List<CartItem> FilterCartItems(
            IEnumerable<CartItem> cartItems,
            ISet<long> selectedIds,
            ISet<long>? selectedSelectionIds = null)
        {
            if (cartItems == null)
            {
                return new List<CartItem>();
            }

            var selectionFilter = selectedSelectionIds ?? new HashSet<long>();
            var hasSelectionFilter = selectionFilter.Count > 0;

            var itemsToProcess = cartItems;

            if (selectedIds != null && selectedIds.Count > 0)
            {
                itemsToProcess = itemsToProcess.Where(ci => ci != null && selectedIds.Contains(ci.Id));
            }

            var result = new List<CartItem>();

            foreach (var cartItem in itemsToProcess)
            {
                if (cartItem == null)
                {
                    continue;
                }

                var hasSelections = cartItem.ProductTypeSelections != null && cartItem.ProductTypeSelections.Any();
                List<CartItemProductType> filteredSelections;

                if (hasSelections)
                {
                    IEnumerable<CartItemProductType> baseSelections = cartItem.ProductTypeSelections
                        .Where(selection => selection != null);

                    if (hasSelectionFilter)
                    {
                        baseSelections = baseSelections.Where(selection => selectionFilter.Contains(selection.Id));
                    }

                    filteredSelections = baseSelections
                        .Select(CloneCartItemSelection)
                        .ToList();

                    if (!filteredSelections.Any())
                    {
                        continue;
                    }
                }
                else
                {
                    filteredSelections = new List<CartItemProductType>();
                }

                var clonedItem = CloneCartItem(cartItem, filteredSelections);
                result.Add(clonedItem);
            }

            return result;
        }

        private static CartItem CloneCartItem(CartItem source, List<CartItemProductType> selections)
        {
            return new CartItem
            {
                Id = source.Id,
                CartId = source.CartId,
                Cart = source.Cart,
                ComboId = source.ComboId,
                Combo = source.Combo,
                ProductId = source.ProductId,
                Product = source.Product,
                Quantity = source.Quantity,
                CreateBy = source.CreateBy,
                CreatedAt = source.CreatedAt,
                UpdatedAt = source.UpdatedAt,
                IsDeleted = source.IsDeleted,
                DeletedAt = source.DeletedAt,
                ProductTypeSelections = selections
            };
        }

        private static CartItemProductType CloneCartItemSelection(CartItemProductType selection)
        {
            return new CartItemProductType
            {
                Id = selection.Id,
                CartItemId = selection.CartItemId,
                CartItem = null,
                ProductTypeId = selection.ProductTypeId,
                ProductType = selection.ProductType,
                Quantity = selection.Quantity,
                UnitPrice = selection.UnitPrice,
                CreateBy = selection.CreateBy,
                CreatedAt = selection.CreatedAt,
                UpdatedAt = selection.UpdatedAt,
                IsDeleted = selection.IsDeleted,
                DeletedAt = selection.DeletedAt
            };
        }

        private static double GetCartItemUnitPrice(CartItem cartItem)
        {
            if (cartItem.Product != null)
            {
                cartItem.Product.RefreshDerivedFields();
                return PriceCalculator.GetProductFinalPrice(cartItem.Product);
            }

            if (cartItem.Combo != null)
            {
                return PriceCalculator.GetComboFinalPrice(cartItem.Combo);
            }

            return 0;
        }

        private static double GetCartItemTotalPrice(CartItem cartItem)
        {
            if (cartItem.Product != null)
            {
                if (cartItem.ProductTypeSelections != null && cartItem.ProductTypeSelections.Any())
                {
                    return cartItem.ProductTypeSelections.Sum(selection => selection.UnitPrice * selection.Quantity);
                }

                cartItem.Product.RefreshDerivedFields();
                var unitPrice = PriceCalculator.GetProductFinalPrice(cartItem.Product);
                return unitPrice * cartItem.Quantity;
            }

            if (cartItem.Combo != null)
            {
                var unitPrice = PriceCalculator.GetComboFinalPrice(cartItem.Combo);
                return unitPrice * cartItem.Quantity;
            }

            return 0;
        }

        private static double GetOrderItemTotalPrice(OrderItem orderItem)
        {
            if (orderItem.ProductTypeSelections != null && orderItem.ProductTypeSelections.Any())
            {
                return orderItem.ProductTypeSelections.Sum(selection => selection.UnitPrice * selection.Quantity);
            }

            return orderItem.Price * orderItem.Quantity;
        }

        private static long GetOrderItemTotalQuantity(OrderItem orderItem)
        {
            if (orderItem.ProductTypeSelections != null && orderItem.ProductTypeSelections.Any())
            {
                return orderItem.ProductTypeSelections.Sum(selection => (long)selection.Quantity);
            }

            return orderItem.Quantity;
        }

        private bool CanManageOrders()
            => User.HasPermission("GetOrderAll");

        private bool CanUpdateOrderStatus()
            => User.HasPermission("ChangeOrderStatusAll");
    }
}

