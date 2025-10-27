using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Assignment.Data;
using Assignment.Extensions;
using Assignment.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Assignment.Controllers.Api
{
    [ApiController]
    [Route("api/materials")]
    [Authorize]
    public class MaterialController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthorizationService _authorizationService;

        public MaterialController(ApplicationDbContext context, IAuthorizationService authorizationService)
        {
            _context = context;
            _authorizationService = authorizationService;
        }

        private string? CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        [HttpGet]
        public async Task<IActionResult> GetMaterials([FromQuery] MaterialQuery query)
        {
            var canGetAll = User.HasPermission("GetMaterialAll");
            var canGetOwn = User.HasPermission("GetMaterial");

            if (!canGetAll && !canGetOwn)
            {
                return Forbid();
            }

            query ??= new MaterialQuery();

            IQueryable<Material> materialsQuery = _context.Materials
                .AsNoTracking()
                .Include(m => m.Unit)
                .Where(m => !m.IsDeleted);

            if (!canGetAll)
            {
                materialsQuery = materialsQuery.Where(m => m.CreateBy == CurrentUserId);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim().ToLowerInvariant();
                materialsQuery = materialsQuery.Where(m =>
                    m.Name.ToLower().Contains(search) ||
                    m.Code.ToLower().Contains(search));
            }

            if (query.UnitId.HasValue)
            {
                materialsQuery = materialsQuery.Where(m => m.UnitId == query.UnitId.Value);
            }

            var materials = await materialsQuery
                .OrderBy(m => m.Name)
                .ThenBy(m => m.Code)
                .ToListAsync();

            return Ok(materials.Select(MapToResponse));
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetMaterial(long id)
        {
            var material = await _context.Materials
                .AsNoTracking()
                .Include(m => m.Unit)
                .FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted);

            if (material == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, material, "GetMaterialPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            return Ok(MapToResponse(material));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CreateMaterialPolicy")]
        public async Task<IActionResult> CreateMaterial([FromBody] MaterialRequest request)
        {
            await ValidateMaterialRequestAsync(request, null);

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var normalizedDescription = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description!.Trim();

            var material = new Material
            {
                Code = string.Empty,
                Name = request.Name!.Trim(),
                Description = normalizedDescription,
                UnitId = request.UnitId!.Value,
                MinStockLevel = request.MinStockLevel ?? 0,
                Price = request.Price ?? 0,
                CreateBy = CurrentUserId,
                CreatedAt = DateTime.Now,
                UpdatedAt = null,
                DeletedAt = null,
                IsDeleted = false
            };

            _context.Materials.Add(material);
            await _context.SaveChangesAsync();

            material.Code = material.Id.ToString();
            _context.Entry(material).Property(m => m.Code).IsModified = true;
            await _context.SaveChangesAsync();

            await _context.Entry(material)
                .Reference(m => m.Unit)
                .LoadAsync();

            return CreatedAtAction(nameof(GetMaterial), new { id = material.Id }, MapToResponse(material));
        }

        [HttpPut("{id:long}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMaterial(long id, [FromBody] MaterialRequest request)
        {
            await ValidateMaterialRequestAsync(request, id);

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var material = await _context.Materials
                .Include(m => m.Unit)
                .FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted);

            if (material == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, material, "UpdateMaterialPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            material.Name = request.Name!.Trim();
            material.Description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description!.Trim();
            material.UnitId = request.UnitId!.Value;
            material.MinStockLevel = request.MinStockLevel ?? 0;
            material.Price = request.Price ?? 0;
            material.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            await _context.Entry(material)
                .Reference(m => m.Unit)
                .LoadAsync();

            return Ok(MapToResponse(material));
        }

        [HttpDelete("{id:long}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMaterial(long id)
        {
            var material = await _context.Materials
                .Include(m => m.Unit)
                .FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted);

            if (material == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, material, "DeleteMaterialPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            material.IsDeleted = true;
            material.DeletedAt = DateTime.Now;
            material.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        private async Task ValidateMaterialRequestAsync(MaterialRequest request, long? materialId)
        {
            if (request == null)
            {
                ModelState.AddModelError(string.Empty, "Yêu cầu không hợp lệ.");
                return;
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                ModelState.AddModelError(nameof(request.Name), "Tên nguyên vật liệu không được để trống.");
            }

            if (!request.UnitId.HasValue || request.UnitId.Value <= 0)
            {
                ModelState.AddModelError(nameof(request.UnitId), "Vui lòng chọn đơn vị hợp lệ.");
            }

            if (request.MinStockLevel.HasValue && request.MinStockLevel.Value < 0)
            {
                ModelState.AddModelError(nameof(request.MinStockLevel), "Mức tồn kho tối thiểu không được nhỏ hơn 0.");
            }

            if (request.Price.HasValue && request.Price.Value < 0)
            {
                ModelState.AddModelError(nameof(request.Price), "Giá không được nhỏ hơn 0.");
            }

            if (!ModelState.IsValid)
            {
                return;
            }

            var normalizedName = request.Name!.Trim();
            var nameExists = await _context.Materials
                .AsNoTracking()
                .Where(m => !m.IsDeleted)
                .AnyAsync(m => m.Name == normalizedName && (!materialId.HasValue || m.Id != materialId.Value));

            if (nameExists)
            {
                ModelState.AddModelError(nameof(request.Name), "Tên nguyên vật liệu đã tồn tại.");
            }

            var unitExists = await _context.Units
                .AsNoTracking()
                .AnyAsync(u => u.Id == request.UnitId!.Value && !u.IsDeleted);

            if (!unitExists)
            {
                ModelState.AddModelError(nameof(request.UnitId), "Đơn vị đã chọn không tồn tại hoặc đã bị xóa.");
            }
        }

        private static MaterialResponse MapToResponse(Material material)
        {
            return new MaterialResponse
            {
                Id = material.Id,
                Code = material.Code,
                Name = material.Name,
                Description = material.Description,
                UnitId = material.UnitId,
                UnitName = material.Unit?.Name,
                MinStockLevel = material.MinStockLevel,
                Price = material.Price,
                CreatedAt = material.CreatedAt,
                UpdatedAt = material.UpdatedAt
            };
        }

        public class MaterialRequest
        {
            public string? Name { get; set; }
            public string? Description { get; set; }
            public long? UnitId { get; set; }
            public decimal? MinStockLevel { get; set; }
            public decimal? Price { get; set; }
        }

        public class MaterialQuery
        {
            public string? Search { get; set; }
            public long? UnitId { get; set; }
        }

        private sealed class MaterialResponse
        {
            public long Id { get; set; }
            public string Code { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
            public long UnitId { get; set; }
            public string? UnitName { get; set; }
            public decimal MinStockLevel { get; set; }
            public decimal Price { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? UpdatedAt { get; set; }
        }
    }
}
