using System;
using Assignment.Models;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Assignment.Data;
using Assignment.Extensions;

namespace Assignment.Controllers
{
    [Authorize]
    public class CategoriesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthorizationService _authorizationService;

        public CategoriesController(ApplicationDbContext context, IAuthorizationService authorizationService)
        {
            _context = context;
            _authorizationService = authorizationService;
        }

        // GET: Categories
        // Hiển thị danh sách các danh mục chưa bị xóa mềm.
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var hasGetAll = User.HasPermission("GetCategoryAll");

            // Bắt đầu câu truy vấn bằng cách lọc ra các bản ghi đã bị xóa mềm.
            IQueryable<Category> categories = _context.Categories.Where(c => !c.IsDeleted);

            if (!hasGetAll)
            {
                if (User.HasPermission("GetCategory"))
                {
                    categories = categories.Where(c => c.CreateBy == userId);
                }
                else
                {
                    return Forbid();
                }
            }

            return View(await categories.ToListAsync());
        }

        // GET: Categories/Details/5
        // Chỉ hiển thị chi tiết nếu danh mục chưa bị xóa.
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Tìm danh mục nếu nó chưa bị xóa mềm.
            var category = await _context.Categories
                .FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted);

            if (category == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, category, "GetCategoryPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            return View(category);
        }

        // GET: Categories/Create
        [Authorize(Policy = "CreateCategoryPolicy")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Categories/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CreateCategoryPolicy")]
        public async Task<IActionResult> Create([Bind("Name,Index")] Category category)
        {
            if (ModelState.IsValid)
            {
                category.CreateBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                category.CreatedAt = DateTime.Now;
                category.UpdatedAt = null;
                category.DeletedAt = null;
                // Đặt cờ IsDeleted thành false một cách tường minh khi tạo mới.
                category.IsDeleted = false;

                _context.Add(category);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        // GET: Categories/Edit/5
        // Chỉ cho phép chỉnh sửa nếu danh mục chưa bị xóa.
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Tìm danh mục nếu nó chưa bị xóa mềm.
            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
            if (category == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, category, "UpdateCategoryPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            return View(category);
        }

        // POST: Categories/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("Name,Index,Id")] Category category)
        {
            if (id != category.Id)
            {
                return NotFound();
            }

            // Đảm bảo danh mục đang được chỉnh sửa tồn tại và chưa bị xóa mềm.
            var existingCategory = await _context.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
            if (existingCategory == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, existingCategory, "UpdateCategoryPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Giữ lại các thông tin gốc (người tạo, ngày tạo, trạng thái xóa).
                    category.CreateBy = existingCategory.CreateBy;
                    category.CreatedAt = existingCategory.CreatedAt;
                    category.IsDeleted = existingCategory.IsDeleted;
                    category.DeletedAt = existingCategory.DeletedAt;

                    // Cập nhật thời gian chỉnh sửa.
                    category.UpdatedAt = DateTime.Now;

                    _context.Update(category);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CategoryExists(category.Id))
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
            return View(category);
        }

        // GET: Categories/Delete/5
        // Hiển thị trang xác nhận xóa nếu danh mục chưa bị xóa.
        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Tìm danh mục nếu nó chưa bị xóa mềm.
            var category = await _context.Categories
                .FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted);

            if (category == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, category, "DeleteCategoryPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            return View(category);
        }

        // POST: Categories/Delete/5
        // Thực hiện xóa mềm.
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            // Tải danh mục cùng với các sản phẩm liên quan để kiểm tra.
            // Chỉ tìm danh mục chưa bị xóa mềm.
            var category = await _context.Categories
                                 .Include(c => c.Products)
                                 .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

            if (category == null)
            {
                // Danh mục có thể đã bị người khác xóa. Chuyển hướng về trang danh sách là an toàn.
                return RedirectToAction(nameof(Index));
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, category, "DeleteCategoryPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            // Kiểm tra xem có sản phẩm nào còn hoạt động liên kết với danh mục này không.
            // Giả định rằng Product cũng có thuộc tính IsDeleted.
            if (category.Products != null && category.Products.Any(p => !p.IsDeleted))
            {
                ModelState.AddModelError(string.Empty, "Không thể xóa danh mục vì vẫn còn sản phẩm liên kết với danh mục này.");
                // Trở lại trang xác nhận xóa với thông báo lỗi.
                return View("Delete", category);
            }

            // Thực hiện xóa mềm bằng cách đặt cờ và dấu thời gian.
            category.IsDeleted = true;
            category.DeletedAt = DateTime.Now;

            _context.Categories.Update(category);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool CategoryExists(long id)
        {
            // Phương thức này cũng cần phải bỏ qua các danh mục đã bị xóa mềm.
            return _context.Categories.Any(e => e.Id == id && !e.IsDeleted);
        }
    }
}
