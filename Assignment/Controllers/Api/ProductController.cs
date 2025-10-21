using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using Assignment.Data;
using Assignment.Enums;
using Assignment.Extensions;
using Assignment.Models;
using Assignment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Assignment.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]s")]
    [Authorize]
    public class ProductController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthorizationService _authorizationService;

        public ProductController(ApplicationDbContext context, IAuthorizationService authorizationService)
        {
            _context = context;
            _authorizationService = authorizationService;
        }

        private string? CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        [HttpGet]
        public async Task<IActionResult> GetProducts()
        {
            var canGetAll = User.HasPermission("GetProductAll");
            var canGetOwn = User.HasPermission("GetProduct");

            if (!canGetAll && !canGetOwn)
            {
                return Forbid();
            }

            IQueryable<Product> query = _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Where(p => !p.IsDeleted);

            if (!canGetAll)
            {
                query = query.Where(p => p.CreateBy == CurrentUserId);
            }

            var products = await query
                .OrderByDescending(p => p.CreatedAt)
                .ThenBy(p => p.Name)
                .ToListAsync();

            return Ok(products.Select(MapToResponse));
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetProduct(long id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

            if (product == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, product, "GetProductPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            return Ok(MapToResponse(product));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CreateProductPolicy")]
        public async Task<IActionResult> CreateProduct([FromBody] ProductRequest request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var product = new Product
            {
                Sold = 0,
                TotalEvaluate = 0,
                AverageEvaluate = 0,
                CreateBy = CurrentUserId,
                CreatedAt = DateTime.Now,
                UpdatedAt = null,
                DeletedAt = null,
                IsDeleted = false
            };

            ApplyRequestToProduct(product, request);

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            await UpdateRelatedComboPrices(product.Id);

            var response = MapToResponse(product);
            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, response);
        }

        [HttpPut("{id:long}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProduct(long id, [FromBody] ProductRequest request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
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

            ApplyRequestToProduct(product, request);
            product.UpdatedAt = DateTime.Now;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await ProductExists(id))
                {
                    return NotFound();
                }

                throw;
            }

            await UpdateRelatedComboPrices(product.Id);

            return Ok(MapToResponse(product));
        }

        [HttpDelete("{id:long}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(long id)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (product == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, product, "DeleteProductPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            var comboLookup = await GetActiveComboNamesByProductIds(new[] { product.Id });
            if (comboLookup.TryGetValue(product.Id, out var combos) && combos.Any())
            {
                return BadRequest(new
                {
                    message = BuildComboDependencyMessage(product.Name, combos),
                    combos
                });
            }

            product.IsDeleted = true;
            product.DeletedAt = DateTime.Now;
            product.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            await UpdateRelatedComboPrices(product.Id);

            return NoContent();
        }

        [HttpPost("bulk-delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteRequest request)
        {
            if (request.Ids == null || request.Ids.Count == 0)
            {
                ModelState.AddModelError(nameof(request.Ids), "Vui lòng chọn ít nhất một sản phẩm để xóa.");
                return ValidationProblem(ModelState);
            }

            var products = await _context.Products
                .Where(p => request.Ids.Contains(p.Id) && !p.IsDeleted)
                .ToListAsync();

            if (!products.Any())
            {
                return NotFound(new { message = "Không tìm thấy sản phẩm hợp lệ để xóa." });
            }

            var productIds = products.Select(p => p.Id).ToList();
            var comboLookup = await GetActiveComboNamesByProductIds(productIds);
            var now = DateTime.Now;
            var deletedCount = 0;
            var unauthorizedCount = 0;
            var affectedProductIds = new HashSet<long>();
            var blockedProducts = new List<object>();

            foreach (var product in products)
            {
                var authResult = await _authorizationService.AuthorizeAsync(User, product, "DeleteProductPolicy");
                if (!authResult.Succeeded)
                {
                    unauthorizedCount++;
                    continue;
                }

                if (comboLookup.TryGetValue(product.Id, out var combos) && combos.Any())
                {
                    blockedProducts.Add(new
                    {
                        productId = product.Id,
                        productName = product.Name,
                        combos
                    });
                    continue;
                }

                product.IsDeleted = true;
                product.DeletedAt = now;
                product.UpdatedAt = now;
                deletedCount++;
                affectedProductIds.Add(product.Id);
            }

            if (deletedCount > 0)
            {
                await _context.SaveChangesAsync();

                foreach (var productId in affectedProductIds)
                {
                    await UpdateRelatedComboPrices(productId);
                }
            }

            if (deletedCount == 0 && unauthorizedCount > 0)
            {
                return Forbid();
            }

            return Ok(new
            {
                deleted = deletedCount,
                unauthorized = unauthorizedCount,
                blocked = blockedProducts.Count,
                blockedProducts
            });
        }

        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await GetAuthorizedCategories();

            var result = categories
                .OrderBy(c => c.Name)
                .Select(c => new
                {
                    c.Id,
                    c.Name
                });

            return Ok(result);
        }

        private static void ApplyRequestToProduct(Product product, ProductRequest request)
        {
            product.Name = request.Name.Trim();
            product.Description = request.Description.Trim();
            product.Price = request.Price;
            product.Stock = request.Stock;
            product.DiscountType = request.DiscountType;
            product.Discount = NormalizeDiscount(request.DiscountType, request.Discount);
            product.IsPublish = request.IsPublish;
            product.ProductImageUrl = request.ProductImageUrl.Trim();
            product.PreparationTime = request.PreparationTime;
            product.Calories = request.Calories;
            product.Ingredients = request.Ingredients.Trim();
            product.IsSpicy = request.IsSpicy;
            product.IsVegetarian = request.IsVegetarian;
            product.CategoryId = request.CategoryId;
        }

        private static long? NormalizeDiscount(DiscountType type, long? value)
        {
            if (type == DiscountType.None)
            {
                return null;
            }

            return value;
        }

        private async Task<List<Category>> GetAuthorizedCategories()
        {
            var hasGetAll = User.HasPermission("GetCategoryAll");
            IQueryable<Category> query = _context.Categories.Where(c => !c.IsDeleted);

            if (hasGetAll)
            {
                return await query.AsNoTracking().ToListAsync();
            }

            if (!User.HasPermission("GetCategory"))
            {
                return new List<Category>();
            }

            return await query.AsNoTracking()
                .Where(c => c.CreateBy == CurrentUserId)
                .ToListAsync();
        }

        private async Task<bool> ProductExists(long id)
        {
            return await _context.Products.AnyAsync(p => p.Id == id && !p.IsDeleted);
        }

        private async Task<Dictionary<long, List<string>>> GetActiveComboNamesByProductIds(IReadOnlyCollection<long> productIds)
        {
            if (productIds == null || productIds.Count == 0)
            {
                return new Dictionary<long, List<string>>();
            }

            var activeCombos = await _context.ComboItems
                .Where(ci => !ci.IsDeleted && productIds.Contains(ci.ProductId))
                .Join(
                    _context.Combos.Where(c => !c.IsDeleted),
                    ci => ci.ComboId,
                    c => c.Id,
                    (ci, combo) => new { ci.ProductId, combo.Name })
                .ToListAsync();

            return activeCombos
                .GroupBy(item => item.ProductId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(item => item.Name)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Distinct()
                        .ToList());
        }

        private static string BuildComboDependencyMessage(string productName, IReadOnlyCollection<string> comboNames)
        {
            if (comboNames == null || comboNames.Count == 0)
            {
                return $"Không thể xóa sản phẩm \"{productName}\" vì đang được sử dụng trong combo khác.";
            }

            var formattedNames = comboNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => $"\"{name.Trim()}\"")
                .ToList();

            if (formattedNames.Count == 0)
            {
                return $"Không thể xóa sản phẩm \"{productName}\" vì đang được sử dụng trong combo khác.";
            }

            var preview = string.Join(", ", formattedNames.Take(3));
            if (formattedNames.Count > 3)
            {
                preview += $" và {formattedNames.Count - 3} combo khác";
            }

            var combosLabel = formattedNames.Count > 1 ? "các combo" : "combo";
            return $"Không thể xóa sản phẩm \"{productName}\" vì đang được sử dụng trong {combosLabel} {preview}.";
        }

        private static ProductResponse MapToResponse(Product product)
        {
            return new ProductResponse
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                Stock = product.Stock,
                Sold = product.Sold,
                DiscountType = product.DiscountType,
                Discount = product.Discount,
                IsPublish = product.IsPublish,
                ProductImageUrl = product.ProductImageUrl,
                PreparationTime = product.PreparationTime,
                Calories = product.Calories,
                Ingredients = product.Ingredients,
                IsSpicy = product.IsSpicy,
                IsVegetarian = product.IsVegetarian,
                TotalEvaluate = product.TotalEvaluate,
                AverageEvaluate = product.AverageEvaluate,
                CategoryId = product.CategoryId,
                CategoryName = product.Category?.Name,
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt
            };
        }

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

        public class ProductRequest
        {
            [Required]
            [StringLength(500)]
            public string Name { get; set; } = string.Empty;

            [Required]
            [StringLength(10000)]
            public string Description { get; set; } = string.Empty;

            [Range(0, double.MaxValue)]
            public double Price { get; set; }

            [Range(0, long.MaxValue)]
            public long Stock { get; set; }

            [Required]
            public DiscountType DiscountType { get; set; }

            [Range(0, long.MaxValue)]
            public long? Discount { get; set; }

            public bool IsPublish { get; set; }

            [Required]
            [StringLength(1000)]
            [DataType(DataType.Url)]
            public string ProductImageUrl { get; set; } = string.Empty;

            [Range(0, long.MaxValue)]
            public long PreparationTime { get; set; }

            [Range(0, long.MaxValue)]
            public long Calories { get; set; }

            [Required]
            [StringLength(1000)]
            public string Ingredients { get; set; } = string.Empty;

            public bool IsSpicy { get; set; }

            public bool IsVegetarian { get; set; }

            [Range(1, long.MaxValue)]
            public long CategoryId { get; set; }
        }

        public class ProductResponse
        {
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public double Price { get; set; }
            public long Stock { get; set; }
            public long Sold { get; set; }
            public DiscountType DiscountType { get; set; }
            public long? Discount { get; set; }
            public bool IsPublish { get; set; }
            public string ProductImageUrl { get; set; } = string.Empty;
            public long PreparationTime { get; set; }
            public long Calories { get; set; }
            public string Ingredients { get; set; } = string.Empty;
            public bool IsSpicy { get; set; }
            public bool IsVegetarian { get; set; }
            public long TotalEvaluate { get; set; }
            public double AverageEvaluate { get; set; }
            public long CategoryId { get; set; }
            public string? CategoryName { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? UpdatedAt { get; set; }
        }

        public class BulkDeleteRequest
        {
            public List<long> Ids { get; set; } = new();
        }
    }
}
