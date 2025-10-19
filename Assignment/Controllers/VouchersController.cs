using Assignment.Data;
using Assignment.Models;
using Assignment.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.IO;
using Assignment.Extensions;
using ClosedXML.Excel;

namespace Assignment.Controllers
{
    [Authorize]
    public class VouchersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthorizationService _authorizationService;
        private readonly UserManager<ApplicationUser> _userManager;

        public VouchersController(ApplicationDbContext context, IAuthorizationService authorizationService, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _authorizationService = authorizationService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var hasGetAll = User.HasPermission("GetVoucherAll");

            IQueryable<Voucher> vouchers = _context.Vouchers.Where(v => !v.IsDeleted);

            if (!hasGetAll)
            {
                if (User.HasPermission("GetVoucher"))
                {
                    vouchers = vouchers.Where(v => v.CreateBy == userId);
                }
                else
                {
                    return Forbid();
                }
            }

            return View(await vouchers.ToListAsync());
        }

        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var voucher = await _context.Vouchers
                .Include(v => v.VoucherUsers)
                .Include(v => v.VoucherProducts)
                .FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted);

            if (voucher == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, voucher, "GetVoucherPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            if (voucher.Type == Enums.VoucherType.Private)
            {
                var activeVoucherUsers = voucher.VoucherUsers?
                    .Where(vu => !vu.IsDeleted)
                    .Select(vu => vu.UserId)
                    .ToList() ?? new List<string>();

                if (activeVoucherUsers.Any())
                {
                    var users = await _userManager.Users
                        .Where(u => activeVoucherUsers.Contains(u.Id))
                        .ToListAsync();

                    var lookup = users.ToDictionary(
                        u => u.Id,
                        u => BuildUserDisplayName(u));

                    ViewData["VoucherUserDisplayNames"] = activeVoucherUsers
                        .Select(id => lookup.TryGetValue(id, out var email) ? email : id)
                        .ToList();
                }
                else
                {
                    ViewData["VoucherUserDisplayNames"] = new List<string>();
                }
            }

            if (voucher.ProductScope == VoucherProductScope.SelectedProducts)
            {
                var productIds = voucher.VoucherProducts?
                    .Where(vp => !vp.IsDeleted)
                    .Select(vp => vp.ProductId)
                    .ToList() ?? new List<long>();

                if (productIds.Any())
                {
                    var products = await _context.Products
                        .Where(p => productIds.Contains(p.Id))
                        .Select(p => new { p.Id, p.Name })
                        .ToListAsync();

                    var lookup = products.ToDictionary(p => p.Id, p => p.Name);

                    ViewData["VoucherProductNames"] = productIds
                        .Select(id => lookup.TryGetValue(id, out var name) ? name : $"Sản phẩm #{id}")
                        .ToList();
                }
                else
                {
                    ViewData["VoucherProductNames"] = new List<string>();
                }
            }

            return View(voucher);
        }

        [Authorize(Policy = "CreateVoucherPolicy")]
        public async Task<IActionResult> Create()
        {
            ViewBag.Users = await GetUserOptionsAsync();
            ViewBag.Products = await GetProductOptionsAsync();
            ViewData["SelectedUserIds"] = new List<string>();
            ViewData["SelectedProductIds"] = new List<long>();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CreateVoucherPolicy")]
        public async Task<IActionResult> Create([Bind("Code,Name,Description,Type,ProductScope,UserId,Discount,DiscountType,Quantity,StartTime,IsLifeTime,EndTime,MinimumRequirements,UnlimitedPercentageDiscount,MaximumPercentageReduction")] Voucher voucher, List<string> UserIds, List<long> ProductIds)
        {
            var codeExists = await _context.Vouchers.AnyAsync(v => v.Code == voucher.Code && !v.IsDeleted);
            if (codeExists)
            {
                ModelState.AddModelError("Code", "Mã voucher này đã tồn tại. Vui lòng chọn một mã khác.");
            }

            var selectedUserIds = UserIds?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList() ?? new List<string>();

            if (voucher.Type == Enums.VoucherType.Private && !selectedUserIds.Any())
            {
                ModelState.AddModelError(string.Empty, "Voucher riêng tư cần ít nhất một người dùng.");
            }

            var selectedProductIds = ProductIds?
                .Where(id => id > 0)
                .Distinct()
                .ToList() ?? new List<long>();

            if (voucher.ProductScope == VoucherProductScope.SelectedProducts && !selectedProductIds.Any())
            {
                ModelState.AddModelError(string.Empty, "Vui lòng chọn ít nhất một sản phẩm áp dụng cho voucher.");
            }

            if (selectedUserIds.Any())
            {
                var validUserIds = await _userManager.Users
                    .Where(u => selectedUserIds.Contains(u.Id))
                    .Select(u => u.Id)
                    .ToListAsync();

                if (validUserIds.Count != selectedUserIds.Count)
                {
                    ModelState.AddModelError(string.Empty, "Có người dùng không hợp lệ trong danh sách đã chọn.");
                }

                selectedUserIds = validUserIds;
            }

            if (selectedProductIds.Any())
            {
                var validProductIds = await _context.Products
                    .Where(p => selectedProductIds.Contains(p.Id) && !p.IsDeleted)
                    .Select(p => p.Id)
                    .ToListAsync();

                if (validProductIds.Count != selectedProductIds.Count)
                {
                    ModelState.AddModelError(string.Empty, "Có sản phẩm không hợp lệ trong danh sách đã chọn.");
                }

                selectedProductIds = validProductIds;
            }

            if (ModelState.IsValid)
            {
                if (voucher.Type == Enums.VoucherType.Public)
                {
                    selectedUserIds.Clear();
                }

                if (voucher.Type == Enums.VoucherType.Public)
                {
                    voucher.UserId = null;
                }

                if (voucher.ProductScope == VoucherProductScope.AllProducts)
                {
                    selectedProductIds.Clear();
                }

                if (voucher.IsLifeTime)
                {
                    voucher.EndTime = null;
                }

                if (voucher.UnlimitedPercentageDiscount)
                {
                    voucher.MaximumPercentageReduction = null;
                }

                voucher.CreateBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                voucher.CreatedAt = DateTime.Now;
                voucher.UpdatedAt = null;
                voucher.DeletedAt = null;
                voucher.IsDeleted = false;

                _context.Add(voucher);
                await _context.SaveChangesAsync();

                if (selectedUserIds.Any())
                {
                    var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    var now = DateTime.Now;

                    foreach (var userId in selectedUserIds)
                    {
                        _context.VoucherUsers.Add(new VoucherUser
                        {
                            VoucherId = voucher.Id,
                            UserId = userId,
                            CreateBy = currentUserId,
                            CreatedAt = now,
                            IsDeleted = false
                        });
                    }

                    await _context.SaveChangesAsync();
                }
                if (selectedProductIds.Any())
                {
                    var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    var now = DateTime.Now;

                    foreach (var productId in selectedProductIds)
                    {
                        _context.VoucherProducts.Add(new VoucherProduct
                        {
                            VoucherId = voucher.Id,
                            ProductId = productId,
                            CreateBy = currentUserId,
                            CreatedAt = now,
                            IsDeleted = false
                        });
                    }

                    await _context.SaveChangesAsync();
                }
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Users = await GetUserOptionsAsync(selectedUserIds);
            ViewBag.Products = await GetProductOptionsAsync(selectedProductIds);
            ViewData["SelectedUserIds"] = selectedUserIds;
            ViewData["SelectedProductIds"] = selectedProductIds;
            return View(voucher);
        }

        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var voucher = await _context.Vouchers
                .Include(v => v.VoucherUsers)
                .Include(v => v.VoucherProducts)
                .FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);
            if (voucher == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, voucher, "UpdateVoucherPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            var selectedUserIds = voucher.VoucherUsers?
                .Where(vu => !vu.IsDeleted)
                .Select(vu => vu.UserId)
                .ToList() ?? new List<string>();

            var selectedProductIds = voucher.VoucherProducts?
                .Where(vp => !vp.IsDeleted)
                .Select(vp => vp.ProductId)
                .ToList() ?? new List<long>();

            ViewBag.Users = await GetUserOptionsAsync(selectedUserIds);
            ViewBag.Products = await GetProductOptionsAsync(selectedProductIds);
            ViewData["SelectedUserIds"] = selectedUserIds;
            ViewData["SelectedProductIds"] = selectedProductIds;
            return View(voucher);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("Code,Name,Description,Type,ProductScope,UserId,Discount,DiscountType,Quantity,StartTime,IsLifeTime,EndTime,MinimumRequirements,UnlimitedPercentageDiscount,MaximumPercentageReduction,Id")] Voucher voucher, List<string> UserIds, List<long> ProductIds)
        {
            if (id != voucher.Id)
            {
                return NotFound();
            }

            var existingVoucher = await _context.Vouchers.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);
            if (existingVoucher == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, existingVoucher, "UpdateVoucherPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            var codeExists = await _context.Vouchers.AnyAsync(v => v.Code == voucher.Code && v.Id != voucher.Id && !v.IsDeleted);
            if (codeExists)
            {
                ModelState.AddModelError("Code", "Mã voucher này đã tồn tại. Vui lòng chọn một mã khác.");
            }

            var selectedUserIds = UserIds?
                .Where(idValue => !string.IsNullOrWhiteSpace(idValue))
                .Distinct()
                .ToList() ?? new List<string>();

            if (voucher.Type == Enums.VoucherType.Private && !selectedUserIds.Any())
            {
                ModelState.AddModelError(string.Empty, "Voucher riêng tư cần ít nhất một người dùng.");
            }

            var selectedProductIds = ProductIds?
                .Where(idValue => idValue > 0)
                .Distinct()
                .ToList() ?? new List<long>();

            if (voucher.ProductScope == VoucherProductScope.SelectedProducts && !selectedProductIds.Any())
            {
                ModelState.AddModelError(string.Empty, "Voucher áp dụng cho một số sản phẩm cần ít nhất một sản phẩm.");
            }

            if (selectedUserIds.Any())
            {
                var validUserIds = await _userManager.Users
                    .Where(u => selectedUserIds.Contains(u.Id))
                    .Select(u => u.Id)
                    .ToListAsync();

                if (validUserIds.Count != selectedUserIds.Count)
                {
                    ModelState.AddModelError(string.Empty, "Có người dùng không hợp lệ trong danh sách đã chọn.");
                }

                selectedUserIds = validUserIds;
            }

            if (selectedProductIds.Any())
            {
                var validProductIds = await _context.Products
                    .Where(p => selectedProductIds.Contains(p.Id) && !p.IsDeleted)
                    .Select(p => p.Id)
                    .ToListAsync();

                if (validProductIds.Count != selectedProductIds.Count)
                {
                    ModelState.AddModelError(string.Empty, "Có sản phẩm không hợp lệ trong danh sách đã chọn.");
                }

                selectedProductIds = validProductIds;
            }

            if (ModelState.IsValid)
            {
                try
                {
                    if (voucher.Type == Enums.VoucherType.Public)
                    {
                        selectedUserIds.Clear();
                    }

                    if (voucher.Type == Enums.VoucherType.Public)
                    {
                        voucher.UserId = null;
                    }

                    if (voucher.ProductScope == VoucherProductScope.AllProducts)
                    {
                        selectedProductIds.Clear();
                    }

                    if (voucher.IsLifeTime)
                    {
                        voucher.EndTime = null;
                    }

                    if (voucher.UnlimitedPercentageDiscount)
                    {
                        voucher.MaximumPercentageReduction = null;
                    }

                    voucher.CreateBy = existingVoucher.CreateBy;
                    voucher.CreatedAt = existingVoucher.CreatedAt;
                    voucher.IsDeleted = existingVoucher.IsDeleted;
                    voucher.DeletedAt = existingVoucher.DeletedAt;

                    voucher.UpdatedAt = DateTime.Now;

                    _context.Update(voucher);
                    await _context.SaveChangesAsync();

                    var existingVoucherUsers = await _context.VoucherUsers
                        .Where(vu => vu.VoucherId == voucher.Id)
                        .ToListAsync();

                    if (existingVoucherUsers.Any())
                    {
                        _context.VoucherUsers.RemoveRange(existingVoucherUsers);
                    }

                    var existingVoucherProducts = await _context.VoucherProducts
                        .Where(vp => vp.VoucherId == voucher.Id)
                        .ToListAsync();

                    if (existingVoucherProducts.Any())
                    {
                        _context.VoucherProducts.RemoveRange(existingVoucherProducts);
                    }

                    if (selectedUserIds.Any())
                    {
                        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                        var now = DateTime.Now;

                        foreach (var userId in selectedUserIds)
                        {
                            _context.VoucherUsers.Add(new VoucherUser
                            {
                                VoucherId = voucher.Id,
                                UserId = userId,
                                CreateBy = currentUserId,
                                CreatedAt = now,
                                IsDeleted = false
                            });
                        }
                    }

                    if (selectedProductIds.Any())
                    {
                        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                        var now = DateTime.Now;

                        foreach (var productId in selectedProductIds)
                        {
                            _context.VoucherProducts.Add(new VoucherProduct
                            {
                                VoucherId = voucher.Id,
                                ProductId = productId,
                                CreateBy = currentUserId,
                                CreatedAt = now,
                                IsDeleted = false
                            });
                        }
                    }

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!VoucherExists(voucher.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Users = await GetUserOptionsAsync(selectedUserIds);
            ViewBag.Products = await GetProductOptionsAsync(selectedProductIds);
            ViewData["SelectedUserIds"] = selectedUserIds;
            ViewData["SelectedProductIds"] = selectedProductIds;
            return View(voucher);
        }

        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var voucher = await _context.Vouchers
                .Include(v => v.VoucherUsers)
                .Include(v => v.VoucherProducts)
                .FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted);

            if (voucher == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, voucher, "DeleteVoucherPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            if (voucher.Type == Enums.VoucherType.Private)
            {
                var userIds = voucher.VoucherUsers?
                    .Where(vu => !vu.IsDeleted)
                    .Select(vu => vu.UserId)
                    .ToList() ?? new List<string>();

                if (userIds.Any())
                {
                    var users = await _userManager.Users
                        .Where(u => userIds.Contains(u.Id))
                        .ToListAsync();

                    var lookup = users.ToDictionary(
                        u => u.Id,
                        u => BuildUserDisplayName(u));

                    ViewData["VoucherUserDisplayNames"] = userIds
                        .Select(id => lookup.TryGetValue(id, out var email) ? email : id)
                        .ToList();
                }
                else
                {
                    ViewData["VoucherUserDisplayNames"] = new List<string>();
                }
            }

            if (voucher.ProductScope == VoucherProductScope.SelectedProducts)
            {
                var productIds = voucher.VoucherProducts?
                    .Where(vp => !vp.IsDeleted)
                    .Select(vp => vp.ProductId)
                    .ToList() ?? new List<long>();

                if (productIds.Any())
                {
                    var products = await _context.Products
                        .Where(p => productIds.Contains(p.Id))
                        .Select(p => new { p.Id, p.Name })
                        .ToListAsync();

                    var lookup = products.ToDictionary(p => p.Id, p => p.Name);

                    ViewData["VoucherProductNames"] = productIds
                        .Select(id => lookup.TryGetValue(id, out var name) ? name : $"Sản phẩm #{id}")
                        .ToList();
                }
                else
                {
                    ViewData["VoucherProductNames"] = new List<string>();
                }
            }

            return View(voucher);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var voucher = await _context.Vouchers
                .Include(v => v.VoucherUsers)
                .Include(v => v.VoucherProducts)
                .FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);

            if (voucher == null)
            {
                return RedirectToAction(nameof(Index));
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, voucher, "DeleteVoucherPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            voucher.IsDeleted = true;
            voucher.DeletedAt = DateTime.Now;

            _context.Vouchers.Update(voucher);

            if (voucher.VoucherUsers != null && voucher.VoucherUsers.Any())
            {
                foreach (var voucherUser in voucher.VoucherUsers)
                {
                    voucherUser.IsDeleted = true;
                    voucherUser.DeletedAt = DateTime.Now;
                }

                _context.VoucherUsers.UpdateRange(voucher.VoucherUsers);
            }

            if (voucher.VoucherProducts != null && voucher.VoucherProducts.Any())
            {
                foreach (var voucherProduct in voucher.VoucherProducts)
                {
                    voucherProduct.IsDeleted = true;
                    voucherProduct.DeletedAt = DateTime.Now;
                }

                _context.VoucherProducts.UpdateRange(voucher.VoucherProducts);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkDelete([FromForm] List<long> selectedIds)
        {
            if (selectedIds == null || selectedIds.Count == 0)
            {
                TempData["Info"] = "Vui lòng chọn ít nhất một mã giảm giá để xóa.";
                return RedirectToAction(nameof(Index));
            }

            var vouchers = await _context.Vouchers
                .Where(v => selectedIds.Contains(v.Id) && !v.IsDeleted)
                .Include(v => v.VoucherUsers)
                .Include(v => v.VoucherProducts)
                .ToListAsync();

            if (!vouchers.Any())
            {
                TempData["Info"] = "Không tìm thấy mã giảm giá hợp lệ để xóa.";
                return RedirectToAction(nameof(Index));
            }

            var now = DateTime.Now;
            var deletableVouchers = new List<Voucher>();
            var voucherUsersToUpdate = new List<VoucherUser>();
            var voucherProductsToUpdate = new List<VoucherProduct>();
            var unauthorizedCount = 0;

            foreach (var voucher in vouchers)
            {
                var authResult = await _authorizationService.AuthorizeAsync(User, voucher, "DeleteVoucherPolicy");
                if (!authResult.Succeeded)
                {
                    unauthorizedCount++;
                    continue;
                }

                voucher.IsDeleted = true;
                voucher.DeletedAt = now;
                voucher.UpdatedAt = now;
                deletableVouchers.Add(voucher);

                if (voucher.VoucherUsers != null)
                {
                    foreach (var voucherUser in voucher.VoucherUsers.Where(vu => !vu.IsDeleted))
                    {
                        voucherUser.IsDeleted = true;
                        voucherUser.DeletedAt = now;
                        voucherUser.UpdatedAt = now;
                        voucherUsersToUpdate.Add(voucherUser);
                    }
                }

                if (voucher.VoucherProducts != null)
                {
                    foreach (var voucherProduct in voucher.VoucherProducts.Where(vp => !vp.IsDeleted))
                    {
                        voucherProduct.IsDeleted = true;
                        voucherProduct.DeletedAt = now;
                        voucherProduct.UpdatedAt = now;
                        voucherProductsToUpdate.Add(voucherProduct);
                    }
                }
            }

            if (deletableVouchers.Any())
            {
                _context.Vouchers.UpdateRange(deletableVouchers);
                if (voucherUsersToUpdate.Any())
                {
                    _context.VoucherUsers.UpdateRange(voucherUsersToUpdate);
                }

                if (voucherProductsToUpdate.Any())
                {
                    _context.VoucherProducts.UpdateRange(voucherProductsToUpdate);
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã xóa {deletableVouchers.Count} mã giảm giá.";
            }
            else
            {
                TempData["Info"] = "Không có mã giảm giá nào được xóa.";
            }

            if (unauthorizedCount > 0)
            {
                var message = $"{unauthorizedCount} mã giảm giá không đủ quyền xóa.";
                var existingError = TempData.ContainsKey("Error") ? TempData["Error"]?.ToString() : null;
                TempData["Error"] = string.IsNullOrWhiteSpace(existingError)
                    ? message
                    : $"{existingError} {message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult DownloadVoucherTemplate()
        {
            if (!User.HasAnyPermission("CreateVoucher", "CreateVoucherAll", "UpdateVoucher", "UpdateVoucherAll"))
            {
                return Forbid();
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("VoucherUsers");
            worksheet.Cell(1, 1).Value = "UserId";
            worksheet.Cell(2, 1).Value = "sample-user-id";

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            var content = stream.ToArray();
            const string fileName = "voucher_users_template.xlsx";
            const string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            return File(content, contentType, fileName);
        }

        private bool VoucherExists(long id)
        {
            return _context.Vouchers.Any(e => e.Id == id && !e.IsDeleted);
        }

        private static string BuildUserDisplayName(ApplicationUser user)
        {
            if (user == null)
            {
                return string.Empty;
            }

            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(user.FullName))
            {
                parts.Add(user.FullName);
            }

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                parts.Add(user.Email);
            }

            if (!string.IsNullOrWhiteSpace(user.UserName) &&
                !string.Equals(user.UserName, user.Id, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(user.UserName, user.Email, StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(user.UserName);
            }

            if (!string.IsNullOrWhiteSpace(user.Id))
            {
                parts.Add($"ID: {user.Id}");
            }

            var distinctParts = new List<string>();
            foreach (var part in parts)
            {
                if (!distinctParts.Any(existing => string.Equals(existing, part, StringComparison.OrdinalIgnoreCase)))
                {
                    distinctParts.Add(part);
                }
            }

            if (!distinctParts.Any())
            {
                return string.Empty;
            }

            if (distinctParts.Count == 1 && distinctParts[0].StartsWith("ID:", StringComparison.OrdinalIgnoreCase))
            {
                return $"Người dùng chưa đặt tên ({distinctParts[0]})";
            }

            var primary = distinctParts[0];
            var extras = distinctParts.Skip(1).ToList();

            return extras.Any()
                ? $"{primary} ({string.Join(", ", extras)})"
                : primary;
        }

        [NonAction]
        private async Task<List<SelectListItem>> GetUserOptionsAsync(IEnumerable<string>? selectedIds = null)
        {
            var selectedSet = selectedIds?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet() ?? new HashSet<string>();

            var users = await _userManager.Users
                .OrderBy(u => u.Email ?? u.UserName ?? u.Id)
                .ToListAsync();

            return users.Select(user => new SelectListItem
            {
                Value = user.Id,
                Text = BuildUserDisplayName(user),
                Selected = selectedSet.Contains(user.Id)
            }).ToList();
        }

        [NonAction]
        private async Task<List<SelectListItem>> GetProductOptionsAsync(IEnumerable<long>? selectedIds = null)
        {
            var selectedSet = selectedIds?
                .Where(id => id > 0)
                .ToHashSet() ?? new HashSet<long>();

            var products = await _context.Products
                .Where(p => !p.IsDeleted)
                .OrderBy(p => p.Name)
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = p.Name,
                    Selected = selectedSet.Contains(p.Id)
                })
                .ToListAsync();

            return products;
        }
    }
}
