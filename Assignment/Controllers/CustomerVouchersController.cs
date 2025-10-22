using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Assignment.Data;
using Assignment.Enums;
using Assignment.Models;
using Assignment.ViewModels;
using Assignment.ViewModels.Vouchers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Assignment.Controllers
{
    [Authorize]
    public class CustomerVouchersController : Controller
    {
        private const int PublicVoucherPageSize = 12;

        private readonly ApplicationDbContext _context;

        public CustomerVouchersController(ApplicationDbContext context)
        {
            _context = context;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Public(int page = 1)
        {
            page = Math.Max(1, page);
            var now = DateTime.Now;

            var query = _context.Vouchers
                .AsNoTracking()
                .Where(v => !v.IsDeleted && v.Type == VoucherType.Public && v.IsPublish && v.IsShow
                    && (v.IsLifeTime || !v.EndTime.HasValue || v.EndTime.Value >= now))
                .OrderBy(v => v.StartTime)
                .ThenBy(v => v.Id);

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)PublicVoucherPageSize);
            if (totalPages > 0 && page > totalPages)
            {
                page = totalPages;
            }

            var vouchers = await query
                .Skip((page - 1) * PublicVoucherPageSize)
                .Take(PublicVoucherPageSize)
                .ToListAsync();

            var savedIds = new HashSet<long>();
            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userId))
                {
                    savedIds = await _context.VoucherUsers
                        .Where(vu => !vu.IsDeleted && vu.IsSaved && vu.UserId == userId)
                        .Select(vu => vu.VoucherId)
                        .ToHashSetAsync();
                }
            }

            var pagedResult = new PagedResult<PublicVoucherViewModel>
            {
                CurrentPage = page,
                PageSize = PublicVoucherPageSize,
                TotalItems = totalItems,
                PageSizeOptions = new[] { PublicVoucherPageSize }
            };

            var items = vouchers
                .Select(v => new PublicVoucherViewModel
                {
                    Id = v.Id,
                    Code = v.Code,
                    Name = v.Name,
                    Description = v.Description,
                    ProductScope = v.ProductScope,
                    ComboScope = v.ComboScope,
                    DiscountType = v.DiscountType,
                    Discount = v.Discount,
                    UnlimitedPercentageDiscount = v.UnlimitedPercentageDiscount,
                    MaximumPercentageReduction = v.MaximumPercentageReduction,
                    MinimumRequirements = v.MinimumRequirements,
                    Quantity = v.Quantity,
                    Used = v.Used,
                    IsLifeTime = v.IsLifeTime,
                    StartTime = v.StartTime,
                    EndTime = v.EndTime,
                    IsSaved = savedIds.Contains(v.Id),
                    IsForNewUsersOnly = v.IsForNewUsersOnly
                })
                .ToList();

            pagedResult.SetItems(items);

            ViewBag.ReferenceTime = now;

            return View(pagedResult.EnsureValidPage());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(long id, int page = 1)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            }

            var voucher = await _context.Vouchers
                .FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted && v.Type == VoucherType.Public && v.IsPublish);

            if (voucher == null || !voucher.IsShow)
            {
                TempData["Error"] = "Voucher không khả dụng để lưu.";
                return RedirectToAction(nameof(Public), new { page });
            }

            var voucherUser = await _context.VoucherUsers
                .FirstOrDefaultAsync(vu => vu.VoucherId == id && vu.UserId == userId);

            if (voucherUser == null)
            {
                voucherUser = new VoucherUser
                {
                    VoucherId = voucher.Id,
                    UserId = userId,
                    CreateBy = userId,
                    CreatedAt = DateTime.Now,
                    IsSaved = true,
                    IsDeleted = false
                };

                _context.VoucherUsers.Add(voucherUser);
            }
            else
            {
                if (voucherUser.IsDeleted)
                {
                    voucherUser.IsDeleted = false;
                    voucherUser.DeletedAt = null;
                }

                voucherUser.IsSaved = true;
                voucherUser.UpdatedAt = DateTime.Now;
                _context.VoucherUsers.Update(voucherUser);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã lưu voucher vào danh sách của bạn.";

            return RedirectToAction(nameof(Public), new { page });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unsave(long id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            }

            var voucherUser = await _context.VoucherUsers
                .FirstOrDefaultAsync(vu => vu.VoucherId == id && vu.UserId == userId && !vu.IsDeleted);

            if (voucherUser == null)
            {
                TempData["Error"] = "Không tìm thấy voucher đã lưu.";
                return RedirectToAction(nameof(Mine));
            }

            if (!voucherUser.IsSaved)
            {
                TempData["Info"] = "Voucher đã được bỏ lưu trước đó.";
                return RedirectToAction(nameof(Mine));
            }

            voucherUser.IsSaved = false;
            voucherUser.UpdatedAt = DateTime.Now;
            _context.VoucherUsers.Update(voucherUser);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã bỏ lưu voucher khỏi danh sách của bạn.";
            return RedirectToAction(nameof(Mine));
        }

        public async Task<IActionResult> Mine(int privatePage = 1, int privatePageSize = 6, int savedPage = 1, int savedPageSize = 6)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            }

            const int defaultPageSize = 6;
            privatePage = Math.Max(1, privatePage);
            savedPage = Math.Max(1, savedPage);
            privatePageSize = privatePageSize > 0 ? privatePageSize : defaultPageSize;
            savedPageSize = savedPageSize > 0 ? savedPageSize : defaultPageSize;

            var now = DateTime.Now;

            var privateQuery = _context.Vouchers
                .AsNoTracking()
                .Where(v => !v.IsDeleted && v.Type == VoucherType.Private &&
                    (v.UserId == userId || v.VoucherUsers.Any(vu => !vu.IsDeleted && !vu.IsSaved && vu.UserId == userId)))
                .OrderBy(v => v.StartTime)
                .ThenBy(v => v.Id);

            var savedQuery = _context.Vouchers
                .AsNoTracking()
                .Where(v => !v.IsDeleted && v.VoucherUsers.Any(vu => !vu.IsDeleted && vu.IsSaved && vu.UserId == userId))
                .OrderBy(v => v.StartTime)
                .ThenBy(v => v.Id);

            var totalPrivate = await privateQuery.CountAsync();
            var totalSaved = await savedQuery.CountAsync();

            var privateTotalPages = totalPrivate > 0 ? (int)Math.Ceiling(totalPrivate / (double)privatePageSize) : 0;
            var savedTotalPages = totalSaved > 0 ? (int)Math.Ceiling(totalSaved / (double)savedPageSize) : 0;

            if (privateTotalPages > 0 && privatePage > privateTotalPages)
            {
                privatePage = privateTotalPages;
            }

            if (savedTotalPages > 0 && savedPage > savedTotalPages)
            {
                savedPage = savedTotalPages;
            }

            if (privateTotalPages == 0)
            {
                privatePage = 1;
            }

            if (savedTotalPages == 0)
            {
                savedPage = 1;
            }

            var privateEntities = await privateQuery
                .Skip((privatePage - 1) * privatePageSize)
                .Take(privatePageSize)
                .ToListAsync();

            var savedEntities = await savedQuery
                .Skip((savedPage - 1) * savedPageSize)
                .Take(savedPageSize)
                .ToListAsync();

            VoucherSummaryViewModel MapVoucher(Voucher v, bool isSaved) => new VoucherSummaryViewModel
            {
                Id = v.Id,
                Code = v.Code,
                Name = v.Name,
                Description = v.Description,
                Type = v.Type,
                ProductScope = v.ProductScope,
                ComboScope = v.ComboScope,
                DiscountType = v.DiscountType,
                Discount = v.Discount,
                UnlimitedPercentageDiscount = v.UnlimitedPercentageDiscount,
                MaximumPercentageReduction = v.MaximumPercentageReduction,
                MinimumRequirements = v.MinimumRequirements,
                Quantity = v.Quantity,
                Used = v.Used,
                IsLifeTime = v.IsLifeTime,
                StartTime = v.StartTime,
                EndTime = v.EndTime,
                IsPublish = v.IsPublish,
                IsShow = v.IsShow,
                IsSaved = isSaved,
                IsForNewUsersOnly = v.IsForNewUsersOnly
            };

            var privateViewModels = privateEntities.Select(v => MapVoucher(v, false)).ToList();
            var savedViewModels = savedEntities.Select(v => MapVoucher(v, true)).ToList();

            var viewModel = new UserVoucherListViewModel
            {
                PrivateVouchers = new PaginatedVoucherCollectionViewModel
                {
                    Items = privateViewModels,
                    CurrentPage = privatePage,
                    PageSize = privatePageSize,
                    TotalItems = totalPrivate
                },
                SavedVouchers = new PaginatedVoucherCollectionViewModel
                {
                    Items = savedViewModels,
                    CurrentPage = savedPage,
                    PageSize = savedPageSize,
                    TotalItems = totalSaved
                }
            };

            ViewBag.ReferenceTime = now;

            return View(viewModel);
        }
    }
}
