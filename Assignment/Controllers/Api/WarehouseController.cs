using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
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
    [Route("api/warehouses")]
    [Authorize]
    public class WarehouseController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public WarehouseController(ApplicationDbContext context)
        {
            _context = context;
        }

        private string? CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        [HttpGet]
        public async Task<IActionResult> GetWarehouses([FromQuery] string? search, CancellationToken cancellationToken)
        {
            var canGetAll = User.HasPermission("GetWarehouseAll");
            var canGetOwn = User.HasPermission("GetWarehouse");

            if (!canGetAll && !canGetOwn)
            {
                return Forbid();
            }

            var query = _context.Warehouses
                .AsNoTracking()
                .Where(w => !w.IsDeleted);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var normalizedSearch = search.Trim().ToLower();
                query = query.Where(w => w.Code.ToLower().Contains(normalizedSearch)
                    || w.Name.ToLower().Contains(normalizedSearch)
                    || (w.Address != null && w.Address.ToLower().Contains(normalizedSearch))
                    || (w.ContactName != null && w.ContactName.ToLower().Contains(normalizedSearch)));
            }

            if (!canGetAll)
            {
                var userId = CurrentUserId;
                query = query.Where(w => w.CreateBy == userId);
            }

            var warehouses = await query
                .OrderBy(w => w.Code)
                .ThenBy(w => w.Name)
                .ToListAsync(cancellationToken);

            return Ok(warehouses.Select(MapToResponse));
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetWarehouse(long id, CancellationToken cancellationToken)
        {
            var warehouse = await _context.Warehouses
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == id && !w.IsDeleted, cancellationToken);

            if (warehouse == null)
            {
                return NotFound();
            }

            if (!User.HasPermission("GetWarehouseAll") && warehouse.CreateBy != CurrentUserId)
            {
                return Forbid();
            }

            return Ok(MapToResponse(warehouse));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CreateWarehousePolicy")]
        public async Task<IActionResult> CreateWarehouse([FromBody] WarehouseRequest request, CancellationToken cancellationToken)
        {
            ValidateRequest(request);

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var normalizedCode = request.Code!.Trim();
            if (await _context.Warehouses.AnyAsync(w => w.Code == normalizedCode && !w.IsDeleted, cancellationToken))
            {
                ModelState.AddModelError(nameof(request.Code), "Mã kho đã tồn tại.");
                return ValidationProblem(ModelState);
            }

            var warehouse = new Warehouse
            {
                Code = normalizedCode,
                Name = request.Name!.Trim(),
                ContactName = request.ContactName?.Trim(),
                PhoneNumber = request.PhoneNumber?.Trim(),
                Email = request.Email?.Trim(),
                Address = request.Address?.Trim(),
                Notes = request.Notes?.Trim(),
                CreateBy = CurrentUserId,
                CreatedAt = DateTime.Now,
                UpdatedAt = null,
                IsDeleted = false,
                DeletedAt = null
            };

            _context.Warehouses.Add(warehouse);
            await _context.SaveChangesAsync(cancellationToken);

            return CreatedAtAction(nameof(GetWarehouse), new { id = warehouse.Id }, MapToResponse(warehouse));
        }

        [HttpPut("{id:long}")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "UpdateWarehousePolicy")]
        public async Task<IActionResult> UpdateWarehouse(long id, [FromBody] WarehouseRequest request, CancellationToken cancellationToken)
        {
            ValidateRequest(request);

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var warehouse = await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == id && !w.IsDeleted, cancellationToken);
            if (warehouse == null)
            {
                return NotFound();
            }

            if (!User.HasPermission("UpdateWarehouseAll") && warehouse.CreateBy != CurrentUserId)
            {
                return Forbid();
            }

            var normalizedCode = request.Code!.Trim();
            if (await _context.Warehouses.AnyAsync(w => w.Id != id && w.Code == normalizedCode && !w.IsDeleted, cancellationToken))
            {
                ModelState.AddModelError(nameof(request.Code), "Mã kho đã tồn tại.");
                return ValidationProblem(ModelState);
            }

            warehouse.Code = normalizedCode;
            warehouse.Name = request.Name!.Trim();
            warehouse.ContactName = request.ContactName?.Trim();
            warehouse.PhoneNumber = request.PhoneNumber?.Trim();
            warehouse.Email = request.Email?.Trim();
            warehouse.Address = request.Address?.Trim();
            warehouse.Notes = request.Notes?.Trim();
            warehouse.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync(cancellationToken);

            return Ok(MapToResponse(warehouse));
        }

        [HttpDelete("{id:long}")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "DeleteWarehousePolicy")]
        public async Task<IActionResult> DeleteWarehouse(long id, CancellationToken cancellationToken)
        {
            var warehouse = await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == id && !w.IsDeleted, cancellationToken);
            if (warehouse == null)
            {
                return NotFound();
            }

            if (!User.HasPermission("DeleteWarehouseAll") && warehouse.CreateBy != CurrentUserId)
            {
                return Forbid();
            }

            warehouse.IsDeleted = true;
            warehouse.DeletedAt = DateTime.Now;
            warehouse.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync(cancellationToken);

            return NoContent();
        }

        private void ValidateRequest(WarehouseRequest? request)
        {
            if (request == null)
            {
                ModelState.AddModelError(string.Empty, "Dữ liệu không hợp lệ.");
                return;
            }

            if (string.IsNullOrWhiteSpace(request.Code))
            {
                ModelState.AddModelError(nameof(request.Code), "Vui lòng nhập mã kho.");
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                ModelState.AddModelError(nameof(request.Name), "Vui lòng nhập tên kho.");
            }
        }

        private static WarehouseResponse MapToResponse(Warehouse warehouse)
        {
            return new WarehouseResponse
            {
                Id = warehouse.Id,
                Code = warehouse.Code,
                Name = warehouse.Name,
                ContactName = warehouse.ContactName,
                PhoneNumber = warehouse.PhoneNumber,
                Email = warehouse.Email,
                Address = warehouse.Address,
                Notes = warehouse.Notes,
                CreateBy = warehouse.CreateBy,
                CreatedAt = warehouse.CreatedAt,
                UpdatedAt = warehouse.UpdatedAt
            };
        }

        public class WarehouseRequest
        {
            [Required]
            [StringLength(100)]
            public string? Code { get; set; }

            [Required]
            [StringLength(255)]
            public string? Name { get; set; }

            [StringLength(255)]
            public string? ContactName { get; set; }

            [StringLength(50)]
            public string? PhoneNumber { get; set; }

            [StringLength(255)]
            [EmailAddress]
            public string? Email { get; set; }

            [StringLength(500)]
            public string? Address { get; set; }

            [StringLength(1000)]
            public string? Notes { get; set; }
        }

        public class WarehouseResponse
        {
            public long Id { get; set; }
            public string Code { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string? ContactName { get; set; }
            public string? PhoneNumber { get; set; }
            public string? Email { get; set; }
            public string? Address { get; set; }
            public string? Notes { get; set; }
            public string? CreateBy { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? UpdatedAt { get; set; }
        }
    }
}
