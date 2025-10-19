using System.ComponentModel.DataAnnotations;
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
                .Select(p => MapToResponse(p))
                .ToListAsync();

            return Ok(products);
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
                Name = request.Name.Trim(),
                Description = request.Description.Trim(),
                Price = request.Price,
                Stock = request.Stock,
                Sold = 0,
                DiscountType = request.DiscountType,
                Discount = NormalizeDiscount(request.DiscountType, request.Discount),
                IsPublish = request.IsPublish,
                ProductImageUrl = request.ProductImageUrl.Trim(),
                PreparationTime = request.PreparationTime,
                Calories = request.Calories,
                Ingredients = request.Ingredients.Trim(),
                IsSpicy = request.IsSpicy,
                IsVegetarian = request.IsVegetarian,
                TotalEvaluate = 0,
                AverageEvaluate = 0,
                CategoryId = request.CategoryId,
                CreateBy = CurrentUserId,
                CreatedAt = DateTime.Now,
                UpdatedAt = null,
                DeletedAt = null,
                IsDeleted = false
            };

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

            product.IsDeleted = true;
            product.DeletedAt = DateTime.Now;
            product.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            await UpdateRelatedComboPrices(product.Id);

            return NoContent();
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

        private object MapToResponse(Product product)
        {
            return new
            {
                product.Id,
                product.Name,
                product.Description,
                product.Price,
                product.Stock,
                product.Sold,
                product.DiscountType,
                product.Discount,
                product.IsPublish,
                product.ProductImageUrl,
                product.PreparationTime,
                product.Calories,
                product.Ingredients,
                product.IsSpicy,
                product.IsVegetarian,
                product.TotalEvaluate,
                product.AverageEvaluate,
                product.CategoryId,
                CategoryName = product.Category?.Name,
                product.CreatedAt,
                product.UpdatedAt
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
    }
}
