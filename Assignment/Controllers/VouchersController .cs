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
    [Authorize] // Thêm Authorize ở cấp controller để yêu cầu đăng nhập cho tất cả các action
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

        // GET: Vouchers
        // Hiển thị danh sách các voucher chưa bị xóa mềm.
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var hasGetAll = User.HasPermission("GetVoucherAll");

            // Bắt đầu câu truy vấn bằng cách lọc ra các bản ghi đã bị xóa mềm.
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

        // GET: Vouchers/Details/5
        // Chỉ hiển thị chi tiết nếu voucher chưa bị xóa.
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Tìm voucher nếu nó chưa bị xóa mềm.
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

            // Tải thông tin người dùng nếu voucher là loại Private
            if (voucher.Type == Enums.VoucherType.Private && !string.IsNullOrEmpty(voucher.UserId))
            {
                var user = await _userManager.FindByIdAsync(voucher.UserId);
                ViewData["UserEmail"] = user?.Email ?? "Unknown User";
            }

            return View(voucher);
        }

        // GET: Vouchers/Create
        [Authorize(Policy = "CreateVoucherPolicy")]
        public async Task<IActionResult> Create()
        {
            var users = await _userManager.Users.ToListAsync();
            ViewData["UserId"] = new SelectList(users, "Id", "Email");
            return View();
        }

        // POST: Vouchers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CreateVoucherPolicy")]
        public async Task<IActionResult> Create([Bind("Code,Name,Description,Type,UserId,Discount,DiscountType,Quantity,StartTime,IsLifeTime,EndTime,MinimumRequirements,UnlimitedPercentageDiscount,MaximumPercentageReduction")] Voucher voucher)
        {
            // Kiểm tra xem mã voucher đã tồn tại và chưa bị xóa hay chưa
            var codeExists = await _context.Vouchers.AnyAsync(v => v.Code == voucher.Code && !v.IsDeleted);
            if (codeExists)
            {
                ModelState.AddModelError("Code", "Mã voucher này đã tồn tại. Vui lòng chọn một mã khác.");
            }

            if (ModelState.IsValid)
            {
                // Xử lý logic cho Type
                if (voucher.Type == Enums.VoucherType.Public)
                {
                    voucher.UserId = null;
                }

                // Xử lý logic cho IsLifeTime
                if (voucher.IsLifeTime)
                {
                    voucher.EndTime = null;
                }

                // Xử lý logic cho UnlimitedPercentageDiscount
                if (voucher.UnlimitedPercentageDiscount)
                {
                    voucher.MaximumPercentageReduction = null;
                }

                voucher.CreateBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                voucher.CreatedAt = DateTime.Now;
                voucher.UpdatedAt = null;
                voucher.DeletedAt = null;
                // Đặt cờ IsDeleted thành false một cách tường minh khi tạo mới.
                voucher.IsDeleted = false;

                _context.Add(voucher);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            // Nếu model không hợp lệ, tải lại danh sách người dùng cho dropdown
            var users = await _userManager.Users.ToListAsync();
            ViewData["UserId"] = new SelectList(users, "Id", "Email", voucher.UserId);
            return View(voucher);
        }

        // GET: Vouchers/Edit/5
        // Chỉ cho phép chỉnh sửa nếu voucher chưa bị xóa.
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Tìm voucher nếu nó chưa bị xóa mềm.
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

        // POST: Vouchers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("Code,Name,Description,Type,UserId,Discount,DiscountType,Quantity,StartTime,IsLifeTime,EndTime,MinimumRequirements,UnlimitedPercentageDiscount,MaximumPercentageReduction,Id")] Voucher voucher)
        {
            if (id != voucher.Id)
            {
                return NotFound();
            }

            // Đảm bảo voucher đang được chỉnh sửa tồn tại và chưa bị xóa mềm.
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

            // Kiểm tra nếu mã voucher mới trùng với một voucher khác (chưa bị xóa)
            var codeExists = await _context.Vouchers.AnyAsync(v => v.Code == voucher.Code && v.Id != voucher.Id && !v.IsDeleted);
            if (codeExists)
            {
                ModelState.AddModelError("Code", "Mã voucher này đã tồn tại. Vui lòng chọn một mã khác.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Xử lý logic cho Type
                    if (voucher.Type == Enums.VoucherType.Public)
                    {
                        voucher.UserId = null;
                    }

                    // Xử lý logic cho IsLifeTime
                    if (voucher.IsLifeTime)
                    {
                        voucher.EndTime = null;
                    }

                    // Xử lý logic cho UnlimitedPercentageDiscount
                    if (voucher.UnlimitedPercentageDiscount)
                    {
                        voucher.MaximumPercentageReduction = null;
                    }

                    // Giữ lại các thông tin gốc (người tạo, ngày tạo, trạng thái xóa).
                    voucher.CreateBy = existingVoucher.CreateBy;
                    voucher.CreatedAt = existingVoucher.CreatedAt;
                    voucher.IsDeleted = existingVoucher.IsDeleted;
                    voucher.DeletedAt = existingVoucher.DeletedAt;

                    // Cập nhật thời gian chỉnh sửa.
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

            // Nếu model không hợp lệ, tải lại danh sách người dùng cho dropdown
            var users = await _userManager.Users.ToListAsync();
            ViewData["UserId"] = new SelectList(users, "Id", "Email", voucher.UserId);
            return View(voucher);
        }

        // GET: Vouchers/Delete/5
        // Hiển thị trang xác nhận xóa nếu voucher chưa bị xóa.
        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Tìm voucher nếu nó chưa bị xóa mềm.
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

            // Tải thông tin người dùng nếu voucher là loại Private
            if (voucher.Type == Enums.VoucherType.Private && !string.IsNullOrEmpty(voucher.UserId))
            {
                var user = await _userManager.FindByIdAsync(voucher.UserId);
                ViewData["UserEmail"] = user?.Email ?? "Unknown User";
            }

            return View(voucher);
        }

        // POST: Vouchers/Delete/5
        // Thực hiện xóa mềm.
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            // Chỉ tìm voucher chưa bị xóa mềm.
            var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);

            if (voucher == null)
            {
                // Voucher có thể đã bị người khác xóa. Chuyển hướng về trang danh sách là an toàn.
                return RedirectToAction(nameof(Index));
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, voucher, "DeleteVoucherPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            // Thực hiện xóa mềm bằng cách đặt cờ và dấu thời gian.
            voucher.IsDeleted = true;
            voucher.DeletedAt = DateTime.Now;

            _context.Vouchers.Update(voucher);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool VoucherExists(long id)
        {
            // Phương thức này cũng cần phải bỏ qua các voucher đã bị xóa mềm.
            return _context.Vouchers.Any(e => e.Id == id && !e.IsDeleted);
        }
    }
}
