using Assignment.Data;
using Assignment.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
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

            if (voucher.Type == Enums.VoucherType.Private && !string.IsNullOrEmpty(voucher.UserId))
            {
                var user = await _userManager.FindByIdAsync(voucher.UserId);
                ViewData["UserEmail"] = user?.Email ?? "Unknown User";
            }

            return View(voucher);
        }

        [Authorize(Policy = "CreateVoucherPolicy")]
        public async Task<IActionResult> Create()
        {
            var users = await _userManager.Users.ToListAsync();
            ViewData["UserId"] = new SelectList(users, "Id", "Email");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CreateVoucherPolicy")]
        public async Task<IActionResult> Create([Bind("Code,Name,Description,Type,UserId,Discount,DiscountType,Quantity,StartTime,IsLifeTime,EndTime,MinimumRequirements,UnlimitedPercentageDiscount,MaximumPercentageReduction")] Voucher voucher)
        {
            var codeExists = await _context.Vouchers.AnyAsync(v => v.Code == voucher.Code && !v.IsDeleted);
            if (codeExists)
            {
                ModelState.AddModelError("Code", "Mã voucher này đã tồn tại. Vui lòng chọn một mã khác.");
            }

            if (ModelState.IsValid)
            {
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
                return RedirectToAction(nameof(Index));
            }

            var users = await _userManager.Users.ToListAsync();
            ViewData["UserId"] = new SelectList(users, "Id", "Email", voucher.UserId);
            return View(voucher);
        }

        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);
            if (voucher == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, voucher, "UpdateVoucherPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            var users = await _userManager.Users.ToListAsync();
            ViewData["UserId"] = new SelectList(users, "Id", "Email", voucher.UserId);
            return View(voucher);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("Code,Name,Description,Type,UserId,Discount,DiscountType,Quantity,StartTime,IsLifeTime,EndTime,MinimumRequirements,UnlimitedPercentageDiscount,MaximumPercentageReduction,Id")] Voucher voucher)
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

            if (ModelState.IsValid)
            {
                try
                {
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

            var users = await _userManager.Users.ToListAsync();
            ViewData["UserId"] = new SelectList(users, "Id", "Email", voucher.UserId);
            return View(voucher);
        }

        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var voucher = await _context.Vouchers
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

            if (voucher.Type == Enums.VoucherType.Private && !string.IsNullOrEmpty(voucher.UserId))
            {
                var user = await _userManager.FindByIdAsync(voucher.UserId);
                ViewData["UserEmail"] = user?.Email ?? "Unknown User";
            }

            return View(voucher);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);

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
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool VoucherExists(long id)
        {
            return _context.Vouchers.Any(e => e.Id == id && !e.IsDeleted);
        }
    }
}
