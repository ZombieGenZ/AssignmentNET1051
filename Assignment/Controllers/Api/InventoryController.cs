using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
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
    [Route("api/inventory")]
    [Authorize(Policy = "ViewInventoryPolicy")]
    public class InventoryController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public InventoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        private string? CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        [HttpGet]
        public async Task<IActionResult> GetInventories(CancellationToken cancellationToken)
        {
            var canViewAll = User.HasPermission("ViewInventoryAll");

            IQueryable<Inventory> query = _context.Inventories
                .AsNoTracking()
                .Where(i => !i.IsDeleted)
                .Include(i => i.Material)!
                    .ThenInclude(m => m!.Unit);

            if (!canViewAll)
            {
                var userId = CurrentUserId;
                query = query.Where(i => i.Material != null && i.Material.CreateBy == userId);
            }

            var inventories = await query
                .OrderBy(i => i.Material!.Name)
                .ThenBy(i => i.MaterialId)
                .ThenBy(i => i.WarehouseId)
                .ToListAsync(cancellationToken);

            var responses = inventories.Select(CreateResponse).ToList();
            return Ok(responses);
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetInventory(long id, CancellationToken cancellationToken)
        {
            var inventory = await _context.Inventories
                .AsNoTracking()
                .Include(i => i.Material)!
                    .ThenInclude(m => m!.Unit)
                .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted, cancellationToken);

            if (inventory == null)
            {
                return NotFound();
            }

            var canViewAll = User.HasPermission("ViewInventoryAll");
            if (!canViewAll)
            {
                var userId = CurrentUserId;
                if (inventory.Material?.CreateBy != userId)
                {
                    return Forbid();
                }
            }

            return Ok(CreateResponse(inventory));
        }

        private static InventoryResponse CreateResponse(Inventory inventory)
        {
            var material = inventory.Material;
            var unit = material?.Unit;
            var minStock = material?.MinStockLevel ?? 0m;
            var currentStock = inventory.CurrentStock;

            return new InventoryResponse
            {
                Id = inventory.Id,
                MaterialId = inventory.MaterialId,
                MaterialName = material?.Name ?? string.Empty,
                WarehouseId = inventory.WarehouseId,
                CurrentStock = currentStock,
                BaseUnitId = material?.UnitId,
                BaseUnitName = unit?.Name,
                MinStockLevel = minStock,
                IsBelowMinimum = currentStock < minStock,
                LastUpdated = inventory.LastUpdated
            };
        }

        public class InventoryResponse
        {
            public long Id { get; set; }
            public long MaterialId { get; set; }
            public string MaterialName { get; set; } = string.Empty;
            public long? WarehouseId { get; set; }
            public decimal CurrentStock { get; set; }
            public long? BaseUnitId { get; set; }
            public string? BaseUnitName { get; set; }
            public decimal MinStockLevel { get; set; }
            public bool IsBelowMinimum { get; set; }
            public DateTime LastUpdated { get; set; }
        }
    }
}
