using System;
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
    [Route("api/suppliers")]
    [Authorize]
    public class SupplierController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public SupplierController(ApplicationDbContext context)
        {
            _context = context;
        }

        private string? CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        [HttpGet]
        public async Task<IActionResult> GetSuppliers([FromQuery] string? search, CancellationToken cancellationToken)
        {
            var canGetAll = User.HasPermission("GetSupplierAll");
            var canGetOwn = User.HasPermission("GetSupplier");

            if (!canGetAll && !canGetOwn)
            {
                return Forbid();
            }

            var query = _context.Suppliers
                .AsNoTracking()
                .Where(s => !s.IsDeleted);

            if (!canGetAll && canGetOwn)
            {
                query = query.Where(s => s.CreateBy == CurrentUserId);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = $"%{search.Trim()}%";
                query = query.Where(s =>
                    EF.Functions.Like(s.Code, keyword) ||
                    EF.Functions.Like(s.Name, keyword) ||
                    (s.ContactName != null && EF.Functions.Like(s.ContactName, keyword)));
            }

            var suppliers = await query
                .OrderBy(s => s.Name)
                .ThenBy(s => s.Code)
                .ToListAsync(cancellationToken);

            var responses = suppliers.Select(MapToResponse).ToList();
            return Ok(responses);
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetSupplier(long id, CancellationToken cancellationToken)
        {
            var supplier = await _context.Suppliers
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, cancellationToken);

            if (supplier == null)
            {
                return NotFound();
            }

            if (!User.HasPermission("GetSupplierAll") && supplier.CreateBy != CurrentUserId)
            {
                return Forbid();
            }

            return Ok(MapToResponse(supplier));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CreateSupplierPolicy")]
        public async Task<IActionResult> CreateSupplier([FromBody] SupplierRequest request, CancellationToken cancellationToken)
        {
            ValidateRequest(request);

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var supplier = new Supplier
            {
                Code = Guid.NewGuid().ToString("N"),
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

            _context.Suppliers.Add(supplier);
            await _context.SaveChangesAsync(cancellationToken);

            supplier.Code = supplier.Id.ToString();
            supplier.UpdatedAt = DateTime.Now;

            _context.Attach(supplier);
            _context.Entry(supplier).Property(s => s.Code).IsModified = true;
            _context.Entry(supplier).Property(s => s.UpdatedAt).IsModified = true;

            await _context.SaveChangesAsync(cancellationToken);

            return CreatedAtAction(nameof(GetSupplier), new { id = supplier.Id }, MapToResponse(supplier));
        }

        [HttpPut("{id:long}")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "UpdateSupplierPolicy")]
        public async Task<IActionResult> UpdateSupplier(long id, [FromBody] SupplierRequest request, CancellationToken cancellationToken)
        {
            ValidateRequest(request);

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, cancellationToken);
            if (supplier == null)
            {
                return NotFound();
            }

            if (!User.HasPermission("UpdateSupplierAll") && supplier.CreateBy != CurrentUserId)
            {
                return Forbid();
            }

            supplier.Name = request.Name!.Trim();
            supplier.ContactName = request.ContactName?.Trim();
            supplier.PhoneNumber = request.PhoneNumber?.Trim();
            supplier.Email = request.Email?.Trim();
            supplier.Address = request.Address?.Trim();
            supplier.Notes = request.Notes?.Trim();
            supplier.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync(cancellationToken);

            return Ok(MapToResponse(supplier));
        }

        [HttpDelete("{id:long}")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "DeleteSupplierPolicy")]
        public async Task<IActionResult> DeleteSupplier(long id, CancellationToken cancellationToken)
        {
            var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, cancellationToken);
            if (supplier == null)
            {
                return NotFound();
            }

            if (!User.HasPermission("DeleteSupplierAll") && supplier.CreateBy != CurrentUserId)
            {
                return Forbid();
            }

            supplier.IsDeleted = true;
            supplier.DeletedAt = DateTime.Now;
            supplier.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync(cancellationToken);

            return NoContent();
        }

        private void ValidateRequest(SupplierRequest? request)
        {
            if (request == null)
            {
                ModelState.AddModelError(string.Empty, "Dữ liệu không hợp lệ.");
                return;
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                ModelState.AddModelError(nameof(request.Name), "Vui lòng nhập tên nhà cung cấp.");
            }
        }

        private static SupplierResponse MapToResponse(Supplier supplier)
        {
            return new SupplierResponse
            {
                Id = supplier.Id,
                Code = supplier.Code,
                Name = supplier.Name,
                ContactName = supplier.ContactName,
                PhoneNumber = supplier.PhoneNumber,
                Email = supplier.Email,
                Address = supplier.Address,
                Notes = supplier.Notes,
                CreateBy = supplier.CreateBy,
                CreatedAt = supplier.CreatedAt,
                UpdatedAt = supplier.UpdatedAt
            };
        }

        public class SupplierRequest
        {
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

        public class SupplierResponse
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
