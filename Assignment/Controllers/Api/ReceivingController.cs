using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Assignment.Data;
using Assignment.Enums;
using Assignment.Extensions;
using Assignment.Models;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
                .Include(n => n.Supplier)
                .Include(n => n.Warehouse)
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
                .Include(n => n.Supplier)
                .Include(n => n.Warehouse)
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

            Supplier? supplier = null;
            if (request.SupplierId.HasValue)
            {
                supplier = await _context.Suppliers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == request.SupplierId && !s.IsDeleted, cancellationToken);

                if (supplier == null)
                {
                    ModelState.AddModelError(nameof(request.SupplierId), "Nhà cung cấp không tồn tại hoặc đã bị xóa.");
                    return ValidationProblem(ModelState);
                }

                if (!User.HasPermission("GetSupplierAll") && supplier.CreateBy != CurrentUserId)
                {
                    return Forbid();
                }
            }

            Warehouse? warehouse = null;
            if (request.WarehouseId.HasValue)
            {
                warehouse = await _context.Warehouses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(w => w.Id == request.WarehouseId && !w.IsDeleted, cancellationToken);

                if (warehouse == null)
                {
                    ModelState.AddModelError(nameof(request.WarehouseId), "Kho không tồn tại hoặc đã bị xóa.");
                    return ValidationProblem(ModelState);
                }

                if (!User.HasPermission("GetWarehouseAll") && warehouse.CreateBy != CurrentUserId)
                {
                    return Forbid();
                }
            }

            var note = new ReceivingNote
            {
                NoteNumber = normalizedNoteNumber,
                Date = request.Date.Date,
                SupplierId = supplier?.Id,
                Supplier = supplier,
                SupplierName = supplier?.Name,
                WarehouseId = warehouse?.Id,
                Warehouse = warehouse,
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
                .Reference(n => n.Supplier)
                .LoadAsync(cancellationToken);

            await _context.Entry(note)
                .Reference(n => n.Warehouse)
                .LoadAsync(cancellationToken);

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
                .Include(n => n.Warehouse)
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

        [HttpGet("details/template")]
        public IActionResult DownloadDetailsTemplate()
        {
            if (!User.HasPermission("CreateReceiving"))
            {
                return Forbid();
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("ReceivingDetails");
            worksheet.Cell(1, 1).Value = "MaterialId";
            worksheet.Cell(1, 2).Value = "Quantity";
            worksheet.Cell(1, 3).Value = "UnitId";
            worksheet.Cell(1, 4).Value = "UnitPrice";
            worksheet.Cell(2, 1).Value = "1";
            worksheet.Cell(2, 2).Value = "10";
            worksheet.Cell(2, 3).Value = "1";
            worksheet.Cell(2, 4).Value = "10000";

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            var content = stream.ToArray();
            const string fileName = "receiving_details_template.xlsx";
            const string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            return File(content, contentType, fileName);
        }

        [HttpPost("details/import")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportDetails([FromForm] IFormFile? file, CancellationToken cancellationToken)
        {
            if (!User.HasPermission("CreateReceiving"))
            {
                return Forbid();
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "Vui lòng chọn file chứa danh sách chi tiết phiếu nhập." });
            }

            var extension = Path.GetExtension(file.FileName);
            if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Vui lòng sử dụng file Excel (.xlsx)." });
            }

            try
            {
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream, cancellationToken);
                stream.Position = 0;

                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheets.FirstOrDefault();
                if (worksheet == null)
                {
                    return BadRequest(new { message = "File không chứa dữ liệu chi tiết phiếu nhập." });
                }

                var detailRequests = new List<ReceivingDetailRequest>();
                var errors = new List<string>();

                foreach (var row in worksheet.RowsUsed())
                {
                    if (row.RowNumber() == 1)
                    {
                        continue;
                    }

                    var materialRaw = row.Cell(1).GetString()?.Trim();
                    if (string.IsNullOrWhiteSpace(materialRaw))
                    {
                        continue;
                    }

                    if (!long.TryParse(materialRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var materialId) || materialId <= 0)
                    {
                        errors.Add($"Dòng {row.RowNumber()}: Mã nguyên vật liệu không hợp lệ.");
                        continue;
                    }

                    decimal quantity;
                    var quantityCell = row.Cell(2);
                    if (quantityCell.TryGetValue(out double quantityNumeric))
                    {
                        quantity = Convert.ToDecimal(quantityNumeric);
                    }
                    else
                    {
                        var quantityRaw = quantityCell.GetString()?.Trim();
                        if (!decimal.TryParse(quantityRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out quantity) || quantity <= 0)
                        {
                            errors.Add($"Dòng {row.RowNumber()}: Số lượng không hợp lệ.");
                            continue;
                        }
                    }

                    decimal unitPrice = 0;
                    var unitPriceCell = row.Cell(4);
                    if (unitPriceCell.TryGetValue(out double priceNumeric))
                    {
                        unitPrice = Convert.ToDecimal(priceNumeric);
                    }
                    else
                    {
                        var priceRaw = unitPriceCell.GetString()?.Trim();
                        if (!string.IsNullOrWhiteSpace(priceRaw) && !decimal.TryParse(priceRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out unitPrice))
                        {
                            errors.Add($"Dòng {row.RowNumber()}: Đơn giá không hợp lệ.");
                            continue;
                        }
                    }

                    var unitRaw = row.Cell(3).GetString()?.Trim();
                    if (!long.TryParse(unitRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unitId) || unitId <= 0)
                    {
                        errors.Add($"Dòng {row.RowNumber()}: Đơn vị không hợp lệ.");
                        continue;
                    }

                    detailRequests.Add(new ReceivingDetailRequest
                    {
                        MaterialId = materialId,
                        Quantity = quantity,
                        UnitId = unitId,
                        UnitPrice = unitPrice < 0 ? 0 : unitPrice
                    });
                }

                if (errors.Count > 0)
                {
                    return BadRequest(new { errors });
                }

                if (detailRequests.Count == 0)
                {
                    return BadRequest(new { message = "Không tìm thấy chi tiết phiếu nhập hợp lệ trong file." });
                }

                var materialIds = detailRequests.Select(d => d.MaterialId).Distinct().ToList();
                var unitIds = detailRequests.Select(d => d.UnitId).Distinct().ToList();

                var materials = await _context.Materials
                    .Where(m => materialIds.Contains(m.Id) && !m.IsDeleted)
                    .Include(m => m.Unit)
                    .ToDictionaryAsync(m => m.Id, cancellationToken);

                if (materials.Count != materialIds.Count)
                {
                    return BadRequest(new { message = "File chứa nguyên vật liệu không tồn tại trong hệ thống." });
                }

                var units = await _context.Units
                    .Where(u => unitIds.Contains(u.Id) && !u.IsDeleted)
                    .ToDictionaryAsync(u => u.Id, cancellationToken);

                if (units.Count != unitIds.Count)
                {
                    return BadRequest(new { message = "File chứa đơn vị không tồn tại trong hệ thống." });
                }

                var invalidRows = new List<string>();
                foreach (var detail in detailRequests)
                {
                    var material = materials[detail.MaterialId];
                    var (success, _) = await TryConvertToBaseQuantityAsync(material, detail.UnitId, detail.Quantity, cancellationToken);
                    if (!success)
                    {
                        invalidRows.Add($"Không tìm thấy quy đổi phù hợp cho nguyên vật liệu {material.Name} (ID: {material.Id}).");
                    }
                }

                if (invalidRows.Count > 0)
                {
                    return BadRequest(new { errors = invalidRows });
                }

                var detailResponses = detailRequests.Select(detail => new
                {
                    materialId = detail.MaterialId,
                    quantity = detail.Quantity,
                    unitId = detail.UnitId,
                    unitPrice = detail.UnitPrice
                }).ToList();

                var materialResponses = materials.Values.Select(material => new
                {
                    id = material.Id,
                    code = material.Code,
                    name = material.Name,
                    unitId = material.UnitId,
                    unitName = material.Unit?.Name
                }).ToList();

                return Ok(new
                {
                    details = detailResponses,
                    materials = materialResponses
                });
            }
            catch
            {
                return BadRequest(new { message = "Không thể đọc dữ liệu từ file. Vui lòng kiểm tra lại định dạng." });
            }
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

            if (!request.WarehouseId.HasValue)
            {
                ModelState.AddModelError(nameof(request.WarehouseId), "Vui lòng chọn kho.");
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
                SupplierCode = note.Supplier?.Code,
                SupplierName = note.SupplierName ?? note.Supplier?.Name,
                WarehouseId = note.WarehouseId,
                WarehouseCode = note.Warehouse?.Code,
                WarehouseName = note.Warehouse?.Name,
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

            public long? SupplierId { get; set; }

            public long? WarehouseId { get; set; }
            public string? WarehouseCode { get; set; }
            public string? WarehouseName { get; set; }

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
            public long? SupplierId { get; set; }
            public string? SupplierCode { get; set; }
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
