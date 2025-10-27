using System;
using System.Collections.Generic;
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
    [Route("api/units")]
    [Authorize]
    public class UnitController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthorizationService _authorizationService;

        public UnitController(ApplicationDbContext context, IAuthorizationService authorizationService)
        {
            _context = context;
            _authorizationService = authorizationService;
        }

        private string? CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        [HttpGet]
        public async Task<IActionResult> GetUnits([FromQuery] bool includeConversions = true)
        {
            var canGetAll = User.HasPermission("GetUnitAll");
            var canGetOwn = User.HasPermission("GetUnit");

            if (!canGetAll && !canGetOwn)
            {
                return Forbid();
            }

            IQueryable<Unit> query = _context.Units
                .AsNoTracking()
                .Where(u => !u.IsDeleted);

            if (!canGetAll)
            {
                query = query.Where(u => u.CreateBy == CurrentUserId);
            }

            if (includeConversions)
            {
                query = query
                    .Include(u => u.Conversions.Where(c => !c.IsDeleted))
                    .ThenInclude(c => c.ToUnit);
            }

            var units = await query
                .OrderBy(u => u.Name)
                .ThenBy(u => u.Id)
                .ToListAsync();

            var responses = units.Select(MapToResponse).ToList();
            return Ok(responses);
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetUnit(long id)
        {
            var unit = await _context.Units
                .AsNoTracking()
                .Include(u => u.Conversions.Where(c => !c.IsDeleted))
                    .ThenInclude(c => c.ToUnit)
                .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);

            if (unit == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, unit, "GetUnitPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            return Ok(MapToResponse(unit));
        }

        [HttpGet("lookup")]
        public async Task<IActionResult> LookupUnits()
        {
            var canGetAll = User.HasPermission("GetUnitAll");
            var canGetOwn = User.HasPermission("GetUnit");

            if (!canGetAll && !canGetOwn)
            {
                return Forbid();
            }

            IQueryable<Unit> query = _context.Units
                .AsNoTracking()
                .Where(u => !u.IsDeleted);

            if (!canGetAll)
            {
                query = query.Where(u => u.CreateBy == CurrentUserId);
            }

            var units = await query
                .OrderBy(u => u.Name)
                .ThenBy(u => u.Id)
                .Select(u => new UnitLookupResponse
                {
                    Id = u.Id,
                    Name = u.Name
                })
                .ToListAsync();

            return Ok(units);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CreateUnitPolicy")]
        public async Task<IActionResult> CreateUnit([FromBody] UnitRequest request)
        {
            await ValidateUnitRequestAsync(request, null);

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var normalizedName = request.Name!.Trim();
            var description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description!.Trim();

            var unit = new Unit
            {
                Name = normalizedName,
                Description = description,
                CreateBy = CurrentUserId,
                CreatedAt = DateTime.Now,
                UpdatedAt = null,
                DeletedAt = null,
                IsDeleted = false
            };

            _context.Units.Add(unit);
            await _context.SaveChangesAsync();

            await SyncConversions(unit, request.Conversions);
            await _context.SaveChangesAsync();

            await _context.Entry(unit)
                .Collection(u => u.Conversions)
                .Query()
                .Where(c => !c.IsDeleted)
                .Include(c => c.ToUnit)
                .LoadAsync();

            return CreatedAtAction(nameof(GetUnit), new { id = unit.Id }, MapToResponse(unit));
        }

        [HttpPut("{id:long}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUnit(long id, [FromBody] UnitRequest request)
        {
            await ValidateUnitRequestAsync(request, id);

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var unit = await _context.Units
                .Include(u => u.Conversions)
                .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);

            if (unit == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, unit, "UpdateUnitPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            unit.Name = request.Name!.Trim();
            unit.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description!.Trim();
            unit.UpdatedAt = DateTime.Now;

            await SyncConversions(unit, request.Conversions);

            await _context.SaveChangesAsync();

            await _context.Entry(unit)
                .Collection(u => u.Conversions)
                .Query()
                .Where(c => !c.IsDeleted)
                .Include(c => c.ToUnit)
                .LoadAsync();

            return Ok(MapToResponse(unit));
        }

        [HttpDelete("{id:long}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUnit(long id)
        {
            var unit = await _context.Units
                .Include(u => u.Conversions)
                .Include(u => u.ConvertedFrom)
                .Include(u => u.Materials)
                .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);

            if (unit == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, unit, "DeleteUnitPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            if (unit.Materials.Any(m => !m.IsDeleted))
            {
                return BadRequest(new
                {
                    message = "Không thể xóa đơn vị vì đang được sử dụng bởi nguyên vật liệu."
                });
            }

            unit.IsDeleted = true;
            unit.DeletedAt = DateTime.Now;
            unit.UpdatedAt = DateTime.Now;

            foreach (var conversion in unit.Conversions.Where(c => !c.IsDeleted))
            {
                conversion.IsDeleted = true;
                conversion.DeletedAt = DateTime.Now;
                conversion.UpdatedAt = DateTime.Now;
            }

            foreach (var conversion in unit.ConvertedFrom.Where(c => !c.IsDeleted))
            {
                conversion.IsDeleted = true;
                conversion.DeletedAt = DateTime.Now;
                conversion.UpdatedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            return NoContent();
        }

        private async Task ValidateUnitRequestAsync(UnitRequest request, long? unitId)
        {
            if (request == null)
            {
                ModelState.AddModelError(string.Empty, "Yêu cầu không hợp lệ.");
                return;
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                ModelState.AddModelError(nameof(request.Name), "Tên đơn vị không được để trống.");
            }

            request.Conversions ??= new List<UnitConversionRequest>();

            var normalizedName = request.Name?.Trim();
            if (!string.IsNullOrEmpty(normalizedName))
            {
                var exists = await _context.Units
                    .AsNoTracking()
                    .Where(u => !u.IsDeleted)
                    .AnyAsync(u => u.Name == normalizedName && (!unitId.HasValue || u.Id != unitId.Value));

                if (exists)
                {
                    ModelState.AddModelError(nameof(request.Name), "Tên đơn vị đã tồn tại.");
                }
            }

            var conversionErrors = new List<string>();
            var seenTargets = new HashSet<long>();
            var validTargetIds = new HashSet<long>();

            foreach (var conversion in request.Conversions)
            {
                if (!conversion.TargetUnitId.HasValue || conversion.TargetUnitId.Value <= 0)
                {
                    conversionErrors.Add("Vui lòng chọn đơn vị chuyển đổi hợp lệ.");
                    continue;
                }

                var targetId = conversion.TargetUnitId.Value;

                if (unitId.HasValue && targetId == unitId.Value)
                {
                    conversionErrors.Add("Không thể tự chuyển đổi sang chính đơn vị này.");
                    continue;
                }

                if (!seenTargets.Add(targetId))
                {
                    conversionErrors.Add("Một đơn vị chuyển đổi không được xuất hiện nhiều lần.");
                    continue;
                }

                if (!conversion.ConversionRate.HasValue || conversion.ConversionRate.Value <= 0)
                {
                    conversionErrors.Add("Giá trị chuyển đổi phải lớn hơn 0.");
                    continue;
                }

                validTargetIds.Add(targetId);
            }

            if (validTargetIds.Count > 0)
            {
                var existingTargetIds = await _context.Units
                    .AsNoTracking()
                    .Where(u => !u.IsDeleted && validTargetIds.Contains(u.Id))
                    .Select(u => u.Id)
                    .ToListAsync();

                if (existingTargetIds.Count != validTargetIds.Count)
                {
                    conversionErrors.Add("Một hoặc nhiều đơn vị chuyển đổi không tồn tại hoặc đã bị xóa.");
                }
            }

            if (conversionErrors.Count > 0)
            {
                ModelState.AddModelError(nameof(request.Conversions), string.Join(" ", conversionErrors));
            }
        }

        private async Task SyncConversions(Unit unit, List<UnitConversionRequest> conversions)
        {
            conversions ??= new List<UnitConversionRequest>();

            var existingConversions = await _context.ConversionUnits
                .Where(c => c.FromUnitId == unit.Id)
                .ToListAsync();

            var validRequests = conversions
                .Where(c => c.TargetUnitId.HasValue && c.TargetUnitId.Value != unit.Id && c.ConversionRate.HasValue && c.ConversionRate.Value > 0)
                .ToDictionary(c => c.TargetUnitId!.Value, c => c);

            var activeConversions = new List<ConversionUnit>();
            var removedTargetIds = new List<long>();

            foreach (var conversion in existingConversions)
            {
                if (!validRequests.TryGetValue(conversion.ToUnitId, out var requestConversion))
                {
                    if (!conversion.IsDeleted)
                    {
                        conversion.IsDeleted = true;
                        conversion.DeletedAt = DateTime.Now;
                        conversion.UpdatedAt = DateTime.Now;
                    }

                    removedTargetIds.Add(conversion.ToUnitId);
                    continue;
                }

                conversion.ConversionRate = requestConversion.ConversionRate!.Value;
                conversion.Description = string.IsNullOrWhiteSpace(requestConversion.Description)
                    ? null
                    : requestConversion.Description!.Trim();
                conversion.UpdatedAt = DateTime.Now;
                conversion.DeletedAt = null;
                conversion.IsDeleted = false;

                activeConversions.Add(conversion);
                validRequests.Remove(conversion.ToUnitId);
            }

            foreach (var remaining in validRequests.Values)
            {
                var conversion = new ConversionUnit
                {
                    FromUnitId = unit.Id,
                    ToUnitId = remaining.TargetUnitId!.Value,
                    ConversionRate = remaining.ConversionRate!.Value,
                    Description = string.IsNullOrWhiteSpace(remaining.Description)
                        ? null
                        : remaining.Description!.Trim(),
                    CreateBy = CurrentUserId,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = null,
                    DeletedAt = null,
                    IsDeleted = false
                };

                _context.ConversionUnits.Add(conversion);
                activeConversions.Add(conversion);
            }

            var activeTargetIds = activeConversions
                .Select(c => c.ToUnitId)
                .ToHashSet();

            if (removedTargetIds.Count > 0)
            {
                var reciprocalsToRemove = await _context.ConversionUnits
                    .Where(c => removedTargetIds.Contains(c.FromUnitId) && c.ToUnitId == unit.Id)
                    .ToListAsync();

                foreach (var reciprocal in reciprocalsToRemove)
                {
                    if (!reciprocal.IsDeleted)
                    {
                        reciprocal.IsDeleted = true;
                        reciprocal.DeletedAt = DateTime.Now;
                        reciprocal.UpdatedAt = DateTime.Now;
                    }
                }
            }

            if (activeTargetIds.Count > 0)
            {
                var reciprocals = await _context.ConversionUnits
                    .Where(c => activeTargetIds.Contains(c.FromUnitId) && c.ToUnitId == unit.Id)
                    .ToListAsync();

                foreach (var conversion in activeConversions)
                {
                    var reciprocalRate = conversion.ConversionRate == 0
                        ? 0
                        : decimal.One / conversion.ConversionRate;

                    var reciprocal = reciprocals.FirstOrDefault(r => r.FromUnitId == conversion.ToUnitId);
                    if (reciprocal == null)
                    {
                        reciprocal = new ConversionUnit
                        {
                            FromUnitId = conversion.ToUnitId,
                            ToUnitId = unit.Id,
                            ConversionRate = reciprocalRate,
                            Description = conversion.Description,
                            CreateBy = CurrentUserId,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = null,
                            DeletedAt = null,
                            IsDeleted = false
                        };

                        _context.ConversionUnits.Add(reciprocal);
                        reciprocals.Add(reciprocal);
                    }
                    else
                    {
                        reciprocal.ConversionRate = reciprocalRate;
                        reciprocal.Description = conversion.Description;
                        reciprocal.UpdatedAt = DateTime.Now;
                        reciprocal.DeletedAt = null;
                        reciprocal.IsDeleted = false;
                    }
                }
            }
        }

        private static UnitResponse MapToResponse(Unit unit)
        {
            return new UnitResponse
            {
                Id = unit.Id,
                Name = unit.Name,
                Description = unit.Description,
                CreatedAt = unit.CreatedAt,
                UpdatedAt = unit.UpdatedAt,
                Conversions = unit.Conversions?
                    .Where(c => !c.IsDeleted && c.ToUnit != null)
                    .Select(c => new UnitConversionResponse
                    {
                        Id = c.Id,
                        TargetUnitId = c.ToUnitId,
                        TargetUnitName = c.ToUnit!.Name,
                        ConversionRate = c.ConversionRate,
                        Description = c.Description
                    })
                    .OrderBy(c => c.TargetUnitName)
                    .ToList() ?? new List<UnitConversionResponse>()
            };
        }

        public class UnitRequest
        {
            public string? Name { get; set; }
            public string? Description { get; set; }
            public List<UnitConversionRequest> Conversions { get; set; } = new();
        }

        public class UnitConversionRequest
        {
            public long? TargetUnitId { get; set; }
            public decimal? ConversionRate { get; set; }
            public string? Description { get; set; }
        }

        private sealed class UnitResponse
        {
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? UpdatedAt { get; set; }
            public List<UnitConversionResponse> Conversions { get; set; } = new();
        }

        private sealed class UnitConversionResponse
        {
            public long Id { get; set; }
            public long TargetUnitId { get; set; }
            public string TargetUnitName { get; set; } = string.Empty;
            public decimal ConversionRate { get; set; }
            public string? Description { get; set; }
        }

        private sealed class UnitLookupResponse
        {
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }
    }
}
