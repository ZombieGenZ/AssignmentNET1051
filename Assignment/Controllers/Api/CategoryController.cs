using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Assignment.Data;
using Assignment.Extensions;
using Assignment.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Assignment.Controllers.Api
{
    [ApiController]
    [Route("api/categories")]
    [Authorize]
    public class CategoryController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthorizationService _authorizationService;

        public CategoryController(ApplicationDbContext context, IAuthorizationService authorizationService)
        {
            _context = context;
            _authorizationService = authorizationService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CategoryResponse>>> GetCategories()
        {
            var hasGetAll = User.HasPermission("GetCategoryAll");
            var hasGetOwn = User.HasPermission("GetCategory");

            if (!hasGetAll && !hasGetOwn)
            {
                return Forbid();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            IQueryable<Category> query = _context.Categories
                .AsNoTracking()
                .Where(c => !c.IsDeleted);

            if (!hasGetAll && hasGetOwn)
            {
                query = query.Where(c => c.CreateBy == userId);
            }

            var categories = await query
                .OrderBy(c => c.Index)
                .ThenBy(c => c.Name)
                .Select(c => new CategoryResponse
                {
                    Id = c.Id,
                    Name = c.Name,
                    Index = c.Index,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt,
                    CreateBy = c.CreateBy
                })
                .ToListAsync();

            return Ok(categories);
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<CategoryResponse>> GetCategory(long id)
        {
            var category = await _context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

            if (category == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, category, "GetCategoryPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            return Ok(MapToResponse(category));
        }

        [HttpPost]
        [Authorize(Policy = "CreateCategoryPolicy")]
        public async Task<ActionResult<CategoryResponse>> CreateCategory([FromBody] CategoryRequest request)
        {
            var trimmedName = request.Name?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                ModelState.AddModelError(nameof(request.Name), "Tên danh mục không được để trống.");
                return ValidationProblem(ModelState);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Forbid();
            }

            var category = new Category
            {
                Name = trimmedName,
                Index = request.Index,
                CreateBy = userId,
                CreatedAt = DateTime.Now,
                UpdatedAt = null,
                DeletedAt = null,
                IsDeleted = false
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, MapToResponse(category));
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<CategoryResponse>> UpdateCategory(long id, [FromBody] CategoryRequest request)
        {
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

            var trimmedName = request.Name?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                ModelState.AddModelError(nameof(request.Name), "Tên danh mục không được để trống.");
                return ValidationProblem(ModelState);
            }

            category.Name = trimmedName;
            category.Index = request.Index;
            category.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(MapToResponse(category));
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> DeleteCategory(long id)
        {
            var category = await _context.Categories
                .Include(c => c.Products)
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

            if (category == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, category, "DeleteCategoryPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            if (category.Products != null && category.Products.Any(p => !p.IsDeleted))
            {
                return Conflict(new ProblemDetails
                {
                    Title = "Không thể xóa danh mục",
                    Detail = "Danh mục vẫn còn sản phẩm đang hoạt động.",
                    Status = StatusCodes.Status409Conflict
                });
            }

            category.IsDeleted = true;
            category.DeletedAt = DateTime.Now;
            category.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        private static CategoryResponse MapToResponse(Category category) => new()
        {
            Id = category.Id,
            Name = category.Name,
            Index = category.Index,
            CreatedAt = category.CreatedAt,
            UpdatedAt = category.UpdatedAt,
            CreateBy = category.CreateBy
        };

        public class CategoryRequest
        {
            [Required]
            [StringLength(100)]
            public string? Name { get; set; }

            [Range(0, long.MaxValue)]
            public long Index { get; set; }
        }

        public class CategoryResponse
        {
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public long Index { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? UpdatedAt { get; set; }
            public string? CreateBy { get; set; }
        }
    }
}
