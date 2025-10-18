using Assignment.Data;
using Assignment.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Assignment.Extensions;

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
                        u => string.IsNullOrWhiteSpace(u.Email) ? (u.UserName ?? u.Id) : u.Email);

                    ViewData["VoucherUserEmails"] = activeVoucherUsers
                        .Select(id => lookup.TryGetValue(id, out var email) ? email : id)
                        .ToList();
                }
                else
                {
                    ViewData["VoucherUserEmails"] = new List<string>();
                }
            }

            return View(voucher);
        }

        [Authorize(Policy = "CreateVoucherPolicy")]
        public async Task<IActionResult> Create()
        {
            ViewBag.Users = await GetUserOptionsAsync();
            ViewData["SelectedUserIds"] = new List<string>();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CreateVoucherPolicy")]
        public async Task<IActionResult> Create([Bind("Code,Name,Description,Type,UserId,Discount,DiscountType,Quantity,StartTime,IsLifeTime,EndTime,MinimumRequirements,UnlimitedPercentageDiscount,MaximumPercentageReduction")] Voucher voucher, List<string> UserIds)
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
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Users = await GetUserOptionsAsync(selectedUserIds);
            ViewData["SelectedUserIds"] = selectedUserIds;
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

            ViewBag.Users = await GetUserOptionsAsync(selectedUserIds);
            ViewData["SelectedUserIds"] = selectedUserIds;
            return View(voucher);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("Code,Name,Description,Type,UserId,Discount,DiscountType,Quantity,StartTime,IsLifeTime,EndTime,MinimumRequirements,UnlimitedPercentageDiscount,MaximumPercentageReduction,Id")] Voucher voucher, List<string> UserIds)
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
            ViewData["SelectedUserIds"] = selectedUserIds;
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
                        u => string.IsNullOrWhiteSpace(u.Email) ? (u.UserName ?? u.Id) : u.Email);

                    ViewData["VoucherUserEmails"] = userIds
                        .Select(id => lookup.TryGetValue(id, out var email) ? email : id)
                        .ToList();
                }
                else
                {
                    ViewData["VoucherUserEmails"] = new List<string>();
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

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult DownloadVoucherTemplate()
        {
            if (!User.HasAnyPermission("CreateVoucher", "CreateVoucherAll", "UpdateVoucher", "UpdateVoucherAll"))
            {
                return Forbid();
            }

            const string fileName = "voucher_users_template.csv";
            var csvBuilder = new StringBuilder();
            csvBuilder.AppendLine("UserId");
            csvBuilder.AppendLine("sample-user-id");

            var bytes = Encoding.UTF8.GetBytes(csvBuilder.ToString());
            return File(bytes, "text/csv", fileName);
        }

        private bool VoucherExists(long id)
        {
            return _context.Vouchers.Any(e => e.Id == id && !e.IsDeleted);
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
                Text = string.IsNullOrWhiteSpace(user.Email) ? (user.UserName ?? user.Id) : user.Email,
                Selected = selectedSet.Contains(user.Id)
            }).ToList();
        }
    }
}
