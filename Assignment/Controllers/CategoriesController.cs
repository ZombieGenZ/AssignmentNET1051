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
using Assignment.Options;
using Assignment.ViewModels;

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

        public async Task<IActionResult> Index(int page = 1, int pageSize = PaginationDefaults.DefaultPageSize)
        {
            page = PaginationDefaults.NormalizePage(page);
            pageSize = PaginationDefaults.NormalizePageSize(pageSize);

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var hasGetAll = User.HasPermission("GetCategoryAll");

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

            var totalItems = await categories.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            if (totalPages > 0 && page > totalPages)
            {
                page = totalPages;
            }

            var pagedCategories = await categories
                .OrderBy(c => c.Index)
                .ThenBy(c => c.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var viewModel = new PagedResult<Category>
            {
                CurrentPage = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                PageSizeOptions = PaginationDefaults.PageSizeOptions
            };

            viewModel.SetItems(pagedCategories);

            return View(viewModel.EnsureValidPage());
        }

        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

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

        [Authorize(Policy = "CreateCategoryPolicy")]
        public IActionResult Create()
        {
            return View();
        }

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
                category.IsDeleted = false;

                _context.Add(category);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("Name,Index,Id")] Category category)
        {
            if (id != category.Id)
            {
                return NotFound();
            }

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
                    category.CreateBy = existingCategory.CreateBy;
                    category.CreatedAt = existingCategory.CreatedAt;
                    category.IsDeleted = existingCategory.IsDeleted;
                    category.DeletedAt = existingCategory.DeletedAt;

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

        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

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

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var category = await _context.Categories
                                 .Include(c => c.Products)
                                 .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

            if (category == null)
            {
                return RedirectToAction(nameof(Index));
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, category, "DeleteCategoryPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            if (category.Products != null && category.Products.Any(p => !p.IsDeleted))
            {
                ModelState.AddModelError(string.Empty, "Không thể xóa danh mục vì vẫn còn sản phẩm liên kết với danh mục này.");
                return View("Delete", category);
            }

            category.IsDeleted = true;
            category.DeletedAt = DateTime.Now;

            _context.Categories.Update(category);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkDelete([FromForm] List<long> selectedIds)
        {
            if (selectedIds == null || selectedIds.Count == 0)
            {
                TempData["Info"] = "Vui lòng chọn ít nhất một danh mục để xóa.";
                return RedirectToAction(nameof(Index));
            }

            var categories = await _context.Categories
                .Where(c => selectedIds.Contains(c.Id) && !c.IsDeleted)
                .Include(c => c.Products)
                .ToListAsync();

            if (!categories.Any())
            {
                TempData["Info"] = "Không tìm thấy danh mục hợp lệ để xóa.";
                return RedirectToAction(nameof(Index));
            }

            var now = DateTime.Now;
            var deletedCategories = new List<Category>();
            var blockedByProducts = new List<string>();
            var unauthorizedCount = 0;

            foreach (var category in categories)
            {
                var authResult = await _authorizationService.AuthorizeAsync(User, category, "DeleteCategoryPolicy");
                if (!authResult.Succeeded)
                {
                    unauthorizedCount++;
                    continue;
                }

                if (category.Products != null && category.Products.Any(p => !p.IsDeleted))
                {
                    blockedByProducts.Add(category.Name);
                    continue;
                }

                category.IsDeleted = true;
                category.DeletedAt = now;
                category.UpdatedAt = now;
                deletedCategories.Add(category);
            }

            if (deletedCategories.Any())
            {
                _context.Categories.UpdateRange(deletedCategories);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã xóa {deletedCategories.Count} danh mục.";
            }
            else if (unauthorizedCount == 0 && !blockedByProducts.Any())
            {
                TempData["Info"] = "Không có danh mục nào được xóa.";
            }

            if (blockedByProducts.Any())
            {
                var message = $"Không thể xóa các danh mục: {string.Join(", ", blockedByProducts)} vì vẫn còn sản phẩm.";
                var existingInfo = TempData.ContainsKey("Info") ? TempData["Info"]?.ToString() : null;
                TempData["Info"] = string.IsNullOrWhiteSpace(existingInfo)
                    ? message
                    : $"{existingInfo} {message}";
            }

            if (unauthorizedCount > 0)
            {
                var message = $"{unauthorizedCount} danh mục không đủ quyền xóa.";
                var existingError = TempData.ContainsKey("Error") ? TempData["Error"]?.ToString() : null;
                TempData["Error"] = string.IsNullOrWhiteSpace(existingError)
                    ? message
                    : $"{existingError} {message}";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool CategoryExists(long id)
        {
            return _context.Categories.Any(e => e.Id == id && !e.IsDeleted);
        }
    }
}
