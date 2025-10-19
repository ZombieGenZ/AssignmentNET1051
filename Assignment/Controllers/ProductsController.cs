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
using Assignment.Enums;
using Assignment.Services;
using Assignment.Extensions;
using Assignment.Options;
using Assignment.ViewModels;

namespace Assignment.Controllers
{
    [Authorize]
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthorizationService _authorizationService;

        public ProductsController(ApplicationDbContext context, IAuthorizationService authorizationService)
        {
            _context = context;
            _authorizationService = authorizationService;
        }

        public async Task<IActionResult> Index(int page = 1, int pageSize = PaginationDefaults.DefaultPageSize)
        {
            page = PaginationDefaults.NormalizePage(page);
            pageSize = PaginationDefaults.NormalizePageSize(pageSize);

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var hasGetAll = User.HasPermission("GetProductAll");

            IQueryable<Product> products = _context.Products
                                                   .Where(p => !p.IsDeleted)
                                                   .Include(p => p.Category);

            if (!hasGetAll)
            {
                if (User.HasPermission("GetProduct"))
                {
                    products = products.Where(p => p.CreateBy == userId);
                }
                else
                {
                    return Forbid();
                }
            }

            var totalItems = await products.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            if (totalPages > 0 && page > totalPages)
            {
                page = totalPages;
            }

            var pagedProducts = await products
                .OrderByDescending(p => p.CreatedAt)
                .ThenBy(p => p.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var viewModel = new PagedResult<Product>
            {
                CurrentPage = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                PageSizeOptions = PaginationDefaults.PageSizeOptions
            };

            viewModel.SetItems(pagedProducts);

            return View(viewModel.EnsureValidPage());
        }

        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted);

            if (product == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, product, "GetProductPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            return View(product);
        }

        [Authorize(Policy = "CreateProductPolicy")]
        public async Task<IActionResult> Create()
        {
            var categories = await GetAuthorizedCategories();
            ViewData["CategoryId"] = new SelectList(categories, "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CreateProductPolicy")]
        public async Task<IActionResult> Create([Bind("Name,Description,Price,Stock,DiscountType,Discount,IsPublish,ProductImageUrl,PreparationTime,Calories,Ingredients,IsSpicy,IsVegetarian,CategoryId")] Product product)
        {
            if (ModelState.IsValid)
            {
                if (product.DiscountType == DiscountType.None)
                {
                    product.Discount = null;
                }

                product.CreateBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                product.CreatedAt = DateTime.Now;
                product.UpdatedAt = null;
                product.DeletedAt = null;
                product.IsDeleted = false;

                _context.Add(product);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            var categories = await GetAuthorizedCategories();
            ViewData["CategoryId"] = new SelectList(categories, "Id", "Name", product.CategoryId);
            return View(product);
        }

        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (product == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, product, "UpdateProductPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            var categories = await GetAuthorizedCategories();
            ViewData["CategoryId"] = new SelectList(categories, "Id", "Name", product.CategoryId);
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("Name,Description,Price,Stock,DiscountType,Discount,IsPublish,ProductImageUrl,PreparationTime,Calories,Ingredients,IsSpicy,IsVegetarian,CategoryId,Id,CreateBy,CreatedAt")] Product product)
        {
            if (id != product.Id)
            {
                return NotFound();
            }

            var existingProduct = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (existingProduct == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, existingProduct, "UpdateProductPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    if (product.DiscountType == DiscountType.None)
                    {
                        product.Discount = null;
                    }

                    product.CreateBy = existingProduct.CreateBy;
                    product.CreatedAt = existingProduct.CreatedAt;
                    product.IsDeleted = existingProduct.IsDeleted;
                    product.DeletedAt = existingProduct.DeletedAt;

                    product.UpdatedAt = DateTime.Now;

                    _context.Update(product);
                    await _context.SaveChangesAsync();

                    await UpdateRelatedComboPrices(product.Id);
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductExists(product.Id))
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
            var categories = await GetAuthorizedCategories();
            ViewData["CategoryId"] = new SelectList(categories, "Id", "Name", product.CategoryId);
            return View(product);
        }

        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted);

            if (product == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, product, "DeleteProductPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            return View(product);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

            if (product == null)
            {
                return RedirectToAction(nameof(Index));
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, product, "DeleteProductPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }




            product.IsDeleted = true;
            product.DeletedAt = DateTime.Now;

            _context.Products.Update(product);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkDelete([FromForm] List<long> selectedIds)
        {
            if (selectedIds == null || selectedIds.Count == 0)
            {
                TempData["Info"] = "Vui lòng chọn ít nhất một sản phẩm để xóa.";
                return RedirectToAction(nameof(Index));
            }

            var products = await _context.Products
                .Where(p => selectedIds.Contains(p.Id) && !p.IsDeleted)
                .ToListAsync();

            if (!products.Any())
            {
                TempData["Info"] = "Không tìm thấy sản phẩm hợp lệ để xóa.";
                return RedirectToAction(nameof(Index));
            }

            var now = DateTime.Now;
            var deletableProducts = new List<Product>();
            var unauthorizedCount = 0;

            foreach (var product in products)
            {
                var authResult = await _authorizationService.AuthorizeAsync(User, product, "DeleteProductPolicy");
                if (!authResult.Succeeded)
                {
                    unauthorizedCount++;
                    continue;
                }

                product.IsDeleted = true;
                product.DeletedAt = now;
                product.UpdatedAt = now;
                deletableProducts.Add(product);
            }

            if (deletableProducts.Any())
            {
                _context.Products.UpdateRange(deletableProducts);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã xóa {deletableProducts.Count} sản phẩm.";
            }
            else
            {
                TempData["Info"] = "Không có sản phẩm nào được xóa.";
            }

            if (unauthorizedCount > 0)
            {
                var message = $"{unauthorizedCount} sản phẩm không đủ quyền xóa.";
                var existingError = TempData.ContainsKey("Error") ? TempData["Error"]?.ToString() : null;
                TempData["Error"] = string.IsNullOrWhiteSpace(existingError)
                    ? message
                    : $"{existingError} {message}";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool ProductExists(long id)
        {
            return _context.Products.Any(e => e.Id == id && !e.IsDeleted);
        }

        [NonAction]
        private async Task<List<Category>> GetAuthorizedCategories()
        {
            var hasGetCategoryAll = User.HasPermission("GetCategoryAll");

            IQueryable<Category> categoriesQuery = _context.Categories.Where(c => !c.IsDeleted);

            if (hasGetCategoryAll)
            {
                return await categoriesQuery.ToListAsync();
            }
            else
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                return await categoriesQuery
                    .Where(c => c.CreateBy == userId)
                    .ToListAsync();
            }
        }

        [NonAction]
        private async Task UpdateRelatedComboPrices(long productId)
        {
            var combos = await _context.Combos
                .Where(c => !c.IsDeleted && c.ComboItems.Any(ci => ci.ProductId == productId && !ci.IsDeleted))
                .Include(c => c.ComboItems)
                .ToListAsync();

            if (!combos.Any())
            {
                return;
            }

            var productIds = combos
                .SelectMany(c => c.ComboItems.Where(ci => !ci.IsDeleted).Select(ci => ci.ProductId))
                .Distinct()
                .ToList();

            var products = await _context.Products
                .Where(p => productIds.Contains(p.Id) && !p.IsDeleted)
                .ToListAsync();

            var productLookup = products.ToDictionary(p => p.Id);

            foreach (var combo in combos)
            {
                var priceItems = combo.ComboItems
                    .Where(ci => !ci.IsDeleted)
                    .Select(ci => productLookup.TryGetValue(ci.ProductId, out var product)
                        ? (product, ci.Quantity)
                        : ((Product?)null, ci.Quantity));

                combo.Price = PriceCalculator.GetComboBasePrice(priceItems);
                combo.UpdatedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();
        }
    }
}
