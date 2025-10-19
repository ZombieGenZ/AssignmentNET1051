using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Linq;
using Assignment.Data;
using Assignment.Extensions;
using Assignment.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Assignment.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]s")]
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

        private string? CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            var canGetAll = User.HasPermission("GetCategoryAll");
            var canGetOwn = User.HasPermission("GetCategory");

            if (!canGetAll && !canGetOwn)
            {
                return Forbid();
            }

            IQueryable<Category> query = _context.Categories
                .AsNoTracking()
                .Where(c => !c.IsDeleted);

            if (!canGetAll)
            {
                query = query.Where(c => c.CreateBy == CurrentUserId);
            }

            var categories = await query
                .OrderBy(c => c.Index)
                .ThenBy(c => c.Name)
                .ToListAsync();

            return Ok(categories.Select(MapToResponse));
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetCategory(long id)
        {
            var category = await _context.Categories
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
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CreateCategoryPolicy")]
        public async Task<IActionResult> CreateCategory([FromBody] CategoryRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                ModelState.AddModelError(nameof(request.Name), "Tên danh mục không được để trống.");
            }

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var normalizedName = request.Name!.Trim();

            var category = new Category
            {
                Name = normalizedName,
                Index = request.Index,
                CreateBy = CurrentUserId,
                CreatedAt = DateTime.Now,
                UpdatedAt = null,
                DeletedAt = null,
                IsDeleted = false
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            var response = MapToResponse(category);
            return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, response);
        }

        [HttpPut("{id:long}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCategory(long id, [FromBody] CategoryRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                ModelState.AddModelError(nameof(request.Name), "Tên danh mục không được để trống.");
            }

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
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

            category.Name = request.Name!.Trim();
            category.Index = request.Index;
            category.UpdatedAt = DateTime.Now;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await CategoryExists(id))
                {
                    return NotFound();
                }

                throw;
            }

            return Ok(MapToResponse(category));
        }

        [HttpDelete("{id:long}")]
        [ValidateAntiForgeryToken]
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
                return BadRequest(new
                {
                    message = "Không thể xóa danh mục vì vẫn còn sản phẩm liên kết với danh mục này."
                });
            }

            category.IsDeleted = true;
            category.DeletedAt = DateTime.Now;
            category.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        private async Task<bool> CategoryExists(long id)
        {
            return await _context.Categories.AnyAsync(c => c.Id == id && !c.IsDeleted);
        }

        private static CategoryResponse MapToResponse(Category category)
        {
            return new CategoryResponse
            {
                Id = category.Id,
                Name = category.Name,
                Index = category.Index,
                CreatedAt = category.CreatedAt,
                UpdatedAt = category.UpdatedAt,
                CreatedBy = category.CreateBy
            };
        }

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
            public string? CreatedBy { get; set; }
        }
    }
}
