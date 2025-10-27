using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Assignment.Data;
using Assignment.Enums;
using Assignment.Extensions;
using Assignment.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Assignment.Controllers.Api
{
    [ApiController]
    [Route("api/receiving-notes")]
    [Authorize]
    public class ReceivingController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ReceivingController(ApplicationDbContext context)
        {
            _context = context;
        }

        private string? CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        [HttpGet]
        public async Task<IActionResult> GetReceivingNotes(CancellationToken cancellationToken)
        {
            if (!User.HasPermission("GetReceivingAll"))
            {
                return Forbid();
            }

            var notes = await _context.ReceivingNotes
                .AsNoTracking()
                .Where(n => !n.IsDeleted)
                .Include(n => n.Details.Where(d => !d.IsDeleted))
                    .ThenInclude(d => d.Material)!
                    .ThenInclude(m => m!.Unit)
                .Include(n => n.Details.Where(d => !d.IsDeleted))
                    .ThenInclude(d => d.Unit)
                .OrderByDescending(n => n.Date)
                .ThenByDescending(n => n.Id)
                .ToListAsync(cancellationToken);

            var responses = notes.Select(MapToResponse).ToList();
            return Ok(responses);
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetReceivingNote(long id, CancellationToken cancellationToken)
        {
            if (!User.HasPermission("GetReceivingAll"))
            {
                return Forbid();
            }

            var note = await _context.ReceivingNotes
                .AsNoTracking()
                .Include(n => n.Details.Where(d => !d.IsDeleted))
                    .ThenInclude(d => d.Material)!
                    .ThenInclude(m => m!.Unit)
                .Include(n => n.Details.Where(d => !d.IsDeleted))
                    .ThenInclude(d => d.Unit)
                .FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted, cancellationToken);

            if (note == null)
            {
                return NotFound();
            }

            return Ok(MapToResponse(note));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CreateReceivingPolicy")]
        public async Task<IActionResult> CreateReceivingNote([FromBody] ReceivingNoteRequest request, CancellationToken cancellationToken)
        {
            ValidateRequest(request);

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var normalizedNoteNumber = string.IsNullOrWhiteSpace(request.NoteNumber)
                ? $"RN-{DateTime.UtcNow:yyyyMMddHHmmss}"
                : request.NoteNumber!.Trim();

            if (await _context.ReceivingNotes.AnyAsync(n => n.NoteNumber == normalizedNoteNumber && !n.IsDeleted, cancellationToken))
            {
                ModelState.AddModelError(nameof(request.NoteNumber), "Số phiếu nhập đã tồn tại.");
                return ValidationProblem(ModelState);
            }

            var materialIds = request.Details.Select(d => d.MaterialId).Distinct().ToList();
            var materials = await _context.Materials
                .Where(m => materialIds.Contains(m.Id) && !m.IsDeleted)
                .Include(m => m.Unit)
                .ToDictionaryAsync(m => m.Id, cancellationToken);

            if (materials.Count != materialIds.Count)
            {
                ModelState.AddModelError(nameof(request.Details), "Không tìm thấy nguyên vật liệu tương ứng.");
                return ValidationProblem(ModelState);
            }

            var unitIds = request.Details.Select(d => d.UnitId).Distinct().ToList();
            var units = await _context.Units
                .Where(u => unitIds.Contains(u.Id) && !u.IsDeleted)
                .ToDictionaryAsync(u => u.Id, cancellationToken);

            if (units.Count != unitIds.Count)
            {
                ModelState.AddModelError(nameof(request.Details), "Không tìm thấy đơn vị tương ứng.");
                return ValidationProblem(ModelState);
            }

            var note = new ReceivingNote
            {
                NoteNumber = normalizedNoteNumber,
                Date = request.Date.Date,
                SupplierId = string.IsNullOrWhiteSpace(request.SupplierId) ? null : request.SupplierId!.Trim(),
                SupplierName = string.IsNullOrWhiteSpace(request.SupplierName) ? null : request.SupplierName!.Trim(),
                WarehouseId = request.WarehouseId,
                Status = request.Status,
                CreateBy = CurrentUserId,
                CreatedAt = DateTime.Now,
                UpdatedAt = null,
                IsDeleted = false,
                DeletedAt = null,
                IsStockApplied = false
            };

            var detailEntities = new List<ReceivingDetail>();
            var index = 0;
            foreach (var detail in request.Details)
            {
                var material = materials[detail.MaterialId];
                var unit = units[detail.UnitId];

                var (success, baseQuantity) = await TryConvertToBaseQuantityAsync(material, detail.UnitId, detail.Quantity, cancellationToken);
                if (!success)
                {
                    ModelState.AddModelError($"Details[{index}].UnitId", "Không tìm thấy quy đổi từ đơn vị nhập sang đơn vị cơ bản của nguyên vật liệu.");
                    index++;
                    continue;
                }

                detailEntities.Add(new ReceivingDetail
                {
                    MaterialId = detail.MaterialId,
                    Material = material,
                    Quantity = detail.Quantity,
                    UnitId = detail.UnitId,
                    Unit = unit,
                    UnitPrice = detail.UnitPrice,
                    BaseQuantity = baseQuantity,
                    CreateBy = CurrentUserId,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = null,
                    IsDeleted = false,
                    DeletedAt = null
                });
                index++;
            }

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            note.Details = detailEntities;

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            _context.ReceivingNotes.Add(note);
            await _context.SaveChangesAsync(cancellationToken);

            if (note.Status == ReceivingNoteStatus.Completed)
            {
                await ApplyInventoryAdjustmentsAsync(note, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            await _context.Entry(note)
                .Collection(n => n.Details)
                .Query()
                .Include(d => d.Material)!.ThenInclude(m => m!.Unit)
                .Include(d => d.Unit)
                .LoadAsync(cancellationToken);

            return CreatedAtAction(nameof(GetReceivingNote), new { id = note.Id }, MapToResponse(note));
        }

        [HttpPost("{id:long}/complete")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CreateReceivingPolicy")]
        public async Task<IActionResult> CompleteReceivingNote(long id, CancellationToken cancellationToken)
        {
            var note = await _context.ReceivingNotes
                .Include(n => n.Details.Where(d => !d.IsDeleted))
                    .ThenInclude(d => d.Material)!
                    .ThenInclude(m => m!.Unit)
                .Include(n => n.Details.Where(d => !d.IsDeleted))
                    .ThenInclude(d => d.Unit)
                .FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted, cancellationToken);

            if (note == null)
            {
                return NotFound();
            }

            if (note.Status == ReceivingNoteStatus.Cancelled)
            {
                ModelState.AddModelError(string.Empty, "Không thể hoàn tất phiếu nhập đã hủy.");
                return ValidationProblem(ModelState);
            }

            if (note.IsStockApplied && note.Status == ReceivingNoteStatus.Completed)
            {
                return Ok(MapToResponse(note));
            }

            note.Status = ReceivingNoteStatus.Completed;
            note.UpdatedAt = DateTime.Now;

            await ApplyInventoryAdjustmentsAsync(note, cancellationToken);

            return Ok(MapToResponse(note));
        }

        private void ValidateRequest(ReceivingNoteRequest request)
        {
            if (request == null)
            {
                ModelState.AddModelError(string.Empty, "Dữ liệu không hợp lệ.");
                return;
            }

            if (request.Details == null || request.Details.Count == 0)
            {
                ModelState.AddModelError(nameof(request.Details), "Phiếu nhập phải có ít nhất một chi tiết.");
            }

            if (!Enum.IsDefined(typeof(ReceivingNoteStatus), request.Status))
            {
                ModelState.AddModelError(nameof(request.Status), "Trạng thái phiếu nhập không hợp lệ.");
            }
        }

        private async Task<(bool Success, decimal BaseQuantity)> TryConvertToBaseQuantityAsync(Material material, long fromUnitId, decimal quantity, CancellationToken cancellationToken)
        {
            if (material.UnitId == fromUnitId)
            {
                return (true, quantity);
            }

            var direct = await _context.ConversionUnits
                .AsNoTracking()
                .Where(c => !c.IsDeleted && c.FromUnitId == fromUnitId && c.ToUnitId == material.UnitId)
                .FirstOrDefaultAsync(cancellationToken);

            if (direct != null)
            {
                return (true, quantity * direct.ConversionRate);
            }

            var reverse = await _context.ConversionUnits
                .AsNoTracking()
                .Where(c => !c.IsDeleted && c.FromUnitId == material.UnitId && c.ToUnitId == fromUnitId)
                .FirstOrDefaultAsync(cancellationToken);

            if (reverse != null && reverse.ConversionRate != 0)
            {
                return (true, quantity / reverse.ConversionRate);
            }

            return (false, 0);
        }

        private async Task ApplyInventoryAdjustmentsAsync(ReceivingNote note, CancellationToken cancellationToken)
        {
            if (note.IsStockApplied || note.Status != ReceivingNoteStatus.Completed)
            {
                return;
            }

            foreach (var detail in note.Details.Where(d => !d.IsDeleted))
            {
                var inventory = await _context.Inventories
                    .FirstOrDefaultAsync(i => i.MaterialId == detail.MaterialId && i.WarehouseId == note.WarehouseId && !i.IsDeleted, cancellationToken);

                if (inventory == null)
                {
                    inventory = new Inventory
                    {
                        MaterialId = detail.MaterialId,
                        WarehouseId = note.WarehouseId,
                        CurrentStock = detail.BaseQuantity,
                        LastUpdated = DateTime.UtcNow,
                        CreateBy = note.CreateBy,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = null,
                        IsDeleted = false,
                        DeletedAt = null
                    };

                    _context.Inventories.Add(inventory);
                }
                else
                {
                    inventory.CurrentStock += detail.BaseQuantity;
                    inventory.LastUpdated = DateTime.UtcNow;
                    inventory.UpdatedAt = DateTime.Now;
                }
            }

            note.IsStockApplied = true;
            note.CompletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
        }

        private static ReceivingNoteResponse MapToResponse(ReceivingNote note)
        {
            var details = note.Details
                .Where(d => !d.IsDeleted)
                .OrderBy(d => d.Id)
                .Select(detail => new ReceivingDetailResponse
                {
                    Id = detail.Id,
                    MaterialId = detail.MaterialId,
                    MaterialName = detail.Material?.Name ?? string.Empty,
                    Quantity = detail.Quantity,
                    UnitId = detail.UnitId,
                    UnitName = detail.Unit?.Name ?? string.Empty,
                    UnitPrice = detail.UnitPrice,
                    BaseQuantity = detail.BaseQuantity
                })
                .ToList();

            return new ReceivingNoteResponse
            {
                Id = note.Id,
                NoteNumber = note.NoteNumber,
                Date = note.Date,
                SupplierId = note.SupplierId,
                SupplierName = note.SupplierName,
                WarehouseId = note.WarehouseId,
                Status = note.Status,
                IsStockApplied = note.IsStockApplied,
                CompletedAt = note.CompletedAt,
                Details = details
            };
        }

        public class ReceivingNoteRequest
        {
            [StringLength(100)]
            public string? NoteNumber { get; set; }

            [Required]
            public DateTime Date { get; set; }

            [StringLength(100)]
            public string? SupplierId { get; set; }

            [StringLength(255)]
            public string? SupplierName { get; set; }

            public long? WarehouseId { get; set; }

            [Required]
            public ReceivingNoteStatus Status { get; set; }

            [Required]
            public List<ReceivingDetailRequest> Details { get; set; } = new();
        }

        public class ReceivingDetailRequest
        {
            [Required]
            public long MaterialId { get; set; }

            [Required]
            [Range(0.0001, double.MaxValue)]
            public decimal Quantity { get; set; }

            [Required]
            public long UnitId { get; set; }

            [Range(0, double.MaxValue)]
            public decimal UnitPrice { get; set; }
        }

        public class ReceivingNoteResponse
        {
            public long Id { get; set; }
            public string NoteNumber { get; set; } = string.Empty;
            public DateTime Date { get; set; }
            public string? SupplierId { get; set; }
            public string? SupplierName { get; set; }
            public long? WarehouseId { get; set; }
            public ReceivingNoteStatus Status { get; set; }
            public bool IsStockApplied { get; set; }
            public DateTime? CompletedAt { get; set; }
            public IReadOnlyList<ReceivingDetailResponse> Details { get; set; } = Array.Empty<ReceivingDetailResponse>();
        }

        public class ReceivingDetailResponse
        {
            public long Id { get; set; }
            public long MaterialId { get; set; }
            public string MaterialName { get; set; } = string.Empty;
            public decimal Quantity { get; set; }
            public long UnitId { get; set; }
            public string UnitName { get; set; } = string.Empty;
            public decimal UnitPrice { get; set; }
            public decimal BaseQuantity { get; set; }
        }
    }
}
