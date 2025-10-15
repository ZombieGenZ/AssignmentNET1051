using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Assignment.Data;
using Assignment.Models;
using Assignment.Enums;
using Assignment.Services;

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

        // GET: Products
        // Hiển thị danh sách các sản phẩm chưa bị xóa mềm.
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var hasGetAll = User.HasClaim(c => c.Type == "GetProductAll");

            // Bắt đầu câu truy vấn bằng cách lọc ra các bản ghi đã bị xóa mềm.
            IQueryable<Product> products = _context.Products
                                                   .Where(p => !p.IsDeleted)
                                                   .Include(p => p.Category);

            if (!hasGetAll)
            {
                if (User.HasClaim(c => c.Type == "GetProduct"))
                {
                    products = products.Where(p => p.CreateBy == userId);
                }
                else
                {
                    return Forbid();
                }
            }

            return View(await products.ToListAsync());
        }

        // GET: Products/Details/5
        // Chỉ hiển thị chi tiết nếu sản phẩm chưa bị xóa.
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Tìm sản phẩm nếu nó chưa bị xóa mềm.
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

        // GET: Products/Create
        [Authorize(Policy = "CreateProductPolicy")]
        public async Task<IActionResult> Create()
        {
            var categories = await GetAuthorizedCategories();
            ViewData["CategoryId"] = new SelectList(categories, "Id", "Name");
            return View();
        }

        // POST: Products/Create
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
                // Đặt cờ IsDeleted thành false một cách tường minh khi tạo mới.
                product.IsDeleted = false;

                _context.Add(product);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            var categories = await GetAuthorizedCategories();
            ViewData["CategoryId"] = new SelectList(categories, "Id", "Name", product.CategoryId);
            return View(product);
        }

        // GET: Products/Edit/5
        // Chỉ cho phép chỉnh sửa nếu sản phẩm chưa bị xóa.
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Tìm sản phẩm nếu nó chưa bị xóa mềm.
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

        // POST: Products/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("Name,Description,Price,Stock,DiscountType,Discount,IsPublish,ProductImageUrl,PreparationTime,Calories,Ingredients,IsSpicy,IsVegetarian,CategoryId,Id,CreateBy,CreatedAt")] Product product)
        {
            if (id != product.Id)
            {
                return NotFound();
            }

            // Đảm bảo sản phẩm đang được chỉnh sửa tồn tại và chưa bị xóa mềm.
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

                    // Giữ lại các thông tin gốc (người tạo, ngày tạo, trạng thái xóa).
                    product.CreateBy = existingProduct.CreateBy;
                    product.CreatedAt = existingProduct.CreatedAt;
                    product.IsDeleted = existingProduct.IsDeleted;
                    product.DeletedAt = existingProduct.DeletedAt;

                    // Cập nhật thời gian chỉnh sửa.
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

        // GET: Products/Delete/5
        // Hiển thị trang xác nhận xóa nếu sản phẩm chưa bị xóa.
        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Tìm sản phẩm nếu nó chưa bị xóa mềm.
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

        // POST: Products/Delete/5
        // Thực hiện xóa mềm.
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            // Chỉ tìm sản phẩm chưa bị xóa mềm.
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

            if (product == null)
            {
                // Sản phẩm có thể đã bị người khác xóa. Chuyển hướng về trang danh sách là an toàn.
                return RedirectToAction(nameof(Index));
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, product, "DeleteProductPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            //if (product.CartItems.Any())
            //{
            //    _context.CartItems.UpdateRange(product.CartItems.Select(ci =>
            //    {
            //        ci.IsDeleted = true;
            //        ci.DeletedAt = DateTime.Now;
            //    }));
            //}

            //if (product.ComboItems.Any())
            //{
            //    _context.ComboItems.UpdateRange(product.ComboItems.Select(ci =>
            //    {
            //        ci.IsDeleted = true;
            //        ci.DeletedAt = DateTime.Now;
            //    }));
            //}


            // Thực hiện xóa mềm bằng cách đặt cờ và dấu thời gian.
            product.IsDeleted = true;
            product.DeletedAt = DateTime.Now;

            _context.Products.Update(product);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ProductExists(long id)
        {
            // Phương thức này cũng cần phải bỏ qua các sản phẩm đã bị xóa mềm.
            return _context.Products.Any(e => e.Id == id && !e.IsDeleted);
        }

        [NonAction]
        private async Task<List<Category>> GetAuthorizedCategories()
        {
            var hasGetCategoryAll = User.HasClaim(c => c.Type == "GetCategoryAll");

            // Chỉ lấy các danh mục chưa bị xóa.
            IQueryable<Category> categoriesQuery = _context.Categories.Where(c => !c.IsDeleted);

            if (hasGetCategoryAll)
            {
                // Load tất cả categories chưa bị xóa.
                return await categoriesQuery.ToListAsync();
            }
            else
            {
                // Chỉ load categories của user hiện tại và chưa bị xóa.
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
                double totalPrice = 0;

                foreach (var item in combo.ComboItems.Where(ci => !ci.IsDeleted))
                {
                    if (productLookup.TryGetValue(item.ProductId, out var product))
                    {
                        totalPrice += PriceCalculator.GetProductFinalPrice(product) * item.Quantity;
                    }
                }

                combo.Price = Math.Round(totalPrice, 2);
                combo.UpdatedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();
        }
    }
}
