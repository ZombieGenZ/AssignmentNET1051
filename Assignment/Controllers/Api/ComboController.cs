using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Security.Claims;
using Assignment.Data;
using Assignment.Enums;
using Assignment.Extensions;
using Assignment.Models;
using Assignment.Services;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Assignment.Controllers.Api
{
    [ApiController]
    [Route("api/combos")]
    [Authorize]
    public class ComboController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthorizationService _authorizationService;

        public ComboController(ApplicationDbContext context, IAuthorizationService authorizationService)
        {
            _context = context;
            _authorizationService = authorizationService;
        }

        private string? CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        [HttpGet]
        public async Task<IActionResult> GetCombos()
        {
            var canGetAll = User.HasPermission("GetComboAll");
            var canGetOwn = User.HasPermission("GetCombo");

            if (!canGetAll && !canGetOwn)
            {
                return Forbid();
            }

            IQueryable<Combo> query = _context.Combos
                .AsNoTracking()
                .Include(c => c.ComboItems)!
                    .ThenInclude(ci => ci.Product)!
                        .ThenInclude(p => p.ProductTypes)
                .Include(c => c.ComboItems)!
                    .ThenInclude(ci => ci.ProductType)
                .Where(c => !c.IsDeleted);

            if (!canGetAll)
            {
                query = query.Where(c => c.CreateBy == CurrentUserId);
            }

            var combos = await query
                .OrderByDescending(c => c.CreatedAt)
                .ThenBy(c => c.Name)
                .ToListAsync();

            return Ok(combos.Select(MapToResponse));
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetCombo(long id)
        {
            var combo = await _context.Combos
                .Include(c => c.ComboItems)!
                    .ThenInclude(ci => ci.Product)!
                        .ThenInclude(p => p.ProductTypes)
                .Include(c => c.ComboItems)!
                    .ThenInclude(ci => ci.ProductType)
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

            if (combo == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, combo, "GetComboPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            return Ok(MapToResponse(combo));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CreateComboPolicy")]
        public async Task<IActionResult> CreateCombo([FromBody] ComboRequest request)
        {
            ValidateComboRequest(request);
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var now = DateTime.Now;
            var combo = new Combo
            {
                Name = request.Name!.Trim(),
                Description = request.Description!.Trim(),
                ImageUrl = request.ImageUrl!.Trim(),
                Stock = request.Stock,
                Index = request.Index,
                DiscountType = request.DiscountType,
                Discount = NormalizeDiscount(request),
                IsPublish = request.IsPublish,
                Sold = 0,
                TotalEvaluate = 0,
                AverageEvaluate = 0,
                CreateBy = CurrentUserId,
                CreatedAt = now,
                UpdatedAt = null,
                DeletedAt = null,
                IsDeleted = false
            };

            var comboItemsResult = await BuildComboItems(request.Items);
            if (!comboItemsResult.Success)
            {
                foreach (var (key, error) in comboItemsResult.Errors)
                {
                    ModelState.AddModelError(key, error);
                }

                return ValidationProblem(ModelState);
            }

            combo.Price = PriceCalculator.GetComboBasePrice(
                comboItemsResult.Items.Select(ci => (ci.Product, ci.ProductType, ci.Quantity)));

            _context.Combos.Add(combo);
            await _context.SaveChangesAsync();

            foreach (var item in comboItemsResult.Items)
            {
                var comboItem = new ComboItem
                {
                    ComboId = combo.Id,
                    ProductId = item.Product!.Id,
                    ProductTypeId = item.ProductType?.Id,
                    Quantity = item.Quantity,
                    CreateBy = CurrentUserId,
                    CreatedAt = now,
                    UpdatedAt = null,
                    DeletedAt = null,
                    IsDeleted = false
                };

                _context.ComboItems.Add(comboItem);
            }

            await _context.SaveChangesAsync();

            var createdCombo = await _context.Combos
                .Include(c => c.ComboItems)!
                    .ThenInclude(ci => ci.Product)!
                        .ThenInclude(p => p.ProductTypes)
                .Include(c => c.ComboItems)!
                    .ThenInclude(ci => ci.ProductType)
                .FirstOrDefaultAsync(c => c.Id == combo.Id);

            return CreatedAtAction(nameof(GetCombo), new { id = combo.Id }, MapToResponse(createdCombo!));
        }

        [HttpPut("{id:long}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCombo(long id, [FromBody] ComboRequest request)
        {
            ValidateComboRequest(request);
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var combo = await _context.Combos
                .Include(c => c.ComboItems)
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

            if (combo == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, combo, "UpdateComboPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            var comboItemsResult = await BuildComboItems(request.Items);
            if (!comboItemsResult.Success)
            {
                foreach (var (key, error) in comboItemsResult.Errors)
                {
                    ModelState.AddModelError(key, error);
                }

                return ValidationProblem(ModelState);
            }

            combo.Name = request.Name!.Trim();
            combo.Description = request.Description!.Trim();
            combo.ImageUrl = request.ImageUrl!.Trim();
            combo.Stock = request.Stock;
            combo.Index = request.Index;
            combo.DiscountType = request.DiscountType;
            combo.Discount = NormalizeDiscount(request);
            combo.IsPublish = request.IsPublish;
            combo.UpdatedAt = DateTime.Now;
            combo.Price = PriceCalculator.GetComboBasePrice(
                comboItemsResult.Items.Select(ci => (ci.Product, ci.ProductType, ci.Quantity)));

            if (combo.ComboItems != null && combo.ComboItems.Any())
            {
                _context.ComboItems.RemoveRange(combo.ComboItems);
            }

            foreach (var item in comboItemsResult.Items)
            {
                _context.ComboItems.Add(new ComboItem
                {
                    ComboId = combo.Id,
                    ProductId = item.Product!.Id,
                    ProductTypeId = item.ProductType?.Id,
                    Quantity = item.Quantity,
                    CreateBy = CurrentUserId,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = null,
                    DeletedAt = null,
                    IsDeleted = false
                });
            }

            await _context.SaveChangesAsync();

            var updatedCombo = await _context.Combos
                .Include(c => c.ComboItems)!
                    .ThenInclude(ci => ci.Product)!
                        .ThenInclude(p => p.ProductTypes)
                .Include(c => c.ComboItems)!
                    .ThenInclude(ci => ci.ProductType)
                .FirstOrDefaultAsync(c => c.Id == combo.Id);

            return Ok(MapToResponse(updatedCombo!));
        }

        [HttpDelete("{id:long}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCombo(long id)
        {
            var combo = await _context.Combos
                .Include(c => c.ComboItems)
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

            if (combo == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, combo, "DeleteComboPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            var now = DateTime.Now;
            combo.IsDeleted = true;
            combo.DeletedAt = now;
            combo.UpdatedAt = now;

            if (combo.ComboItems != null)
            {
                foreach (var item in combo.ComboItems.Where(ci => !ci.IsDeleted))
                {
                    item.IsDeleted = true;
                    item.DeletedAt = now;
                    item.UpdatedAt = now;
                }
            }

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("bulk-delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteRequest request)
        {
            if (request.Ids == null || request.Ids.Count == 0)
            {
                ModelState.AddModelError(nameof(request.Ids), "Vui lòng chọn ít nhất một combo để xóa.");
                return ValidationProblem(ModelState);
            }

            var combos = await _context.Combos
                .Where(c => request.Ids.Contains(c.Id) && !c.IsDeleted)
                .Include(c => c.ComboItems)
                .ToListAsync();

            if (!combos.Any())
            {
                return NotFound(new { message = "Không tìm thấy combo hợp lệ để xóa." });
            }

            var now = DateTime.Now;
            var deletedCount = 0;
            var unauthorizedCount = 0;

            foreach (var combo in combos)
            {
                var authResult = await _authorizationService.AuthorizeAsync(User, combo, "DeleteComboPolicy");
                if (!authResult.Succeeded)
                {
                    unauthorizedCount++;
                    continue;
                }

                combo.IsDeleted = true;
                combo.DeletedAt = now;
                combo.UpdatedAt = now;

                if (combo.ComboItems != null)
                {
                    foreach (var item in combo.ComboItems.Where(ci => !ci.IsDeleted))
                    {
                        item.IsDeleted = true;
                        item.DeletedAt = now;
                        item.UpdatedAt = now;
                    }
                }

                deletedCount++;
            }

            if (deletedCount == 0)
            {
                return Forbid();
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                deleted = deletedCount,
                unauthorized = unauthorizedCount
            });
        }

        [HttpPost("bulk-publish")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkPublish([FromBody] BulkPublishRequest request)
        {
            if (request?.Ids == null || request.Ids.Count == 0)
            {
                ModelState.AddModelError(nameof(request.Ids), "Vui lòng chọn ít nhất một combo để cập nhật.");
                return ValidationProblem(ModelState);
            }

            var combos = await _context.Combos
                .Where(c => request.Ids.Contains(c.Id) && !c.IsDeleted)
                .ToListAsync();

            if (!combos.Any())
            {
                return NotFound(new { message = "Không tìm thấy combo hợp lệ để cập nhật." });
            }

            var updatedCount = 0;
            var unauthorizedCount = 0;
            var now = DateTime.Now;

            foreach (var combo in combos)
            {
                var authResult = await _authorizationService.AuthorizeAsync(User, combo, "UpdateComboPolicy");
                if (!authResult.Succeeded)
                {
                    unauthorizedCount++;
                    continue;
                }

                if (combo.IsPublish == request.IsPublish)
                {
                    continue;
                }

                combo.IsPublish = request.IsPublish;
                combo.UpdatedAt = now;
                updatedCount++;
            }

            if (updatedCount > 0)
            {
                await _context.SaveChangesAsync();
            }

            return Ok(new
            {
                updated = updatedCount,
                unauthorized = unauthorizedCount
            });
        }

        [HttpGet("products")]
        public async Task<IActionResult> GetProducts()
        {
            var products = await GetAuthorizedProducts();
            var result = products
                .OrderBy(p => p.Name)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    FinalPrice = PriceCalculator.GetProductFinalPrice(p),
                    p.ProductImageUrl,
                    ProductTypes = (p.ProductTypes ?? new List<ProductType>())
                        .Where(pt => !pt.IsDeleted)
                        .OrderBy(pt => pt.Name)
                        .Select(pt => new
                        {
                            pt.Id,
                            pt.Name,
                            pt.Price,
                            FinalPrice = PriceCalculator.GetProductTypeFinalPrice(pt),
                            pt.DiscountType,
                            pt.Discount,
                            pt.IsPublish
                        })
                });

            return Ok(result);
        }

        [HttpGet("template")]
        public IActionResult DownloadTemplate()
        {
            if (!User.HasAnyPermission("CreateCombo", "CreateComboAll", "UpdateCombo", "UpdateComboAll"))
            {
                return Forbid();
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("ComboItems");
            worksheet.Cell(1, 1).Value = "ProductId";
            worksheet.Cell(1, 2).Value = "Quantity";
            worksheet.Cell(2, 1).Value = "1";
            worksheet.Cell(2, 2).Value = "1";

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            var content = stream.ToArray();
            const string fileName = "combo_items_template.xlsx";
            const string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            return File(content, contentType, fileName);
        }

        [HttpPost("import-items")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportComboItems([FromForm] IFormFile? file)
        {
            if (!User.HasAnyPermission("CreateCombo", "CreateComboAll", "UpdateCombo", "UpdateComboAll"))
            {
                return Forbid();
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "Vui lòng chọn file chứa danh sách sản phẩm." });
            }

            var extension = Path.GetExtension(file.FileName);
            if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Vui lòng sử dụng file Excel (.xlsx)." });
            }

            try
            {
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                stream.Position = 0;

                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheets.FirstOrDefault();
                if (worksheet == null)
                {
                    return BadRequest(new { message = "File không chứa dữ liệu sản phẩm." });
                }

                var itemQuantities = new Dictionary<long, long>();
                var invalidEntries = new List<string>();

                foreach (var row in worksheet.RowsUsed())
                {
                    var productRaw = row.Cell(1).GetString()?.Trim();
                    if (string.IsNullOrWhiteSpace(productRaw))
                    {
                        continue;
                    }

                    if (row.RowNumber() == 1 && string.Equals(productRaw, "ProductId", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!long.TryParse(productRaw, out var productId) || productId <= 0)
                    {
                        invalidEntries.Add(productRaw);
                        continue;
                    }

                    var quantityCell = row.Cell(2);
                    long quantity;
                    if (quantityCell.TryGetValue(out double numericQuantity))
                    {
                        quantity = (long)Math.Round(numericQuantity);
                    }
                    else
                    {
                        var quantityRaw = quantityCell.GetString()?.Trim();
                        if (string.IsNullOrWhiteSpace(quantityRaw))
                        {
                            quantity = 1;
                        }
                        else if (!long.TryParse(quantityRaw, out quantity))
                        {
                            invalidEntries.Add($"{productId} (số lượng không hợp lệ)");
                            continue;
                        }
                    }

                    if (quantity <= 0)
                    {
                        invalidEntries.Add($"{productId} (số lượng không hợp lệ)");
                        continue;
                    }

                    if (itemQuantities.ContainsKey(productId))
                    {
                        itemQuantities[productId] += quantity;
                    }
                    else
                    {
                        itemQuantities[productId] = quantity;
                    }
                }

                if (itemQuantities.Count == 0)
                {
                    return Ok(new ComboItemsImportResponse
                    {
                        InvalidEntries = invalidEntries
                    });
                }

                var productIds = itemQuantities.Keys.ToList();
                var authorizedProducts = await GetAuthorizedProducts();
                var productLookup = authorizedProducts.ToDictionary(p => p.Id);

                var items = new List<ComboItemImport>();
                var products = new List<ComboItemProduct>();

                foreach (var productId in productIds)
                {
                    if (!productLookup.TryGetValue(productId, out var product))
                    {
                        invalidEntries.Add(productId.ToString());
                        continue;
                    }

                    items.Add(new ComboItemImport
                    {
                        ProductId = productId,
                        Quantity = itemQuantities[productId]
                    });

                    products.Add(new ComboItemProduct
                    {
                        Id = product.Id,
                        Name = product.Name,
                        FinalPrice = PriceCalculator.GetProductFinalPrice(product),
                        ProductImageUrl = product.ProductImageUrl,
                        ProductTypes = (product.ProductTypes ?? new List<ProductType>())
                            .Where(pt => !pt.IsDeleted)
                            .Select(pt => new ComboItemProductType
                            {
                                Id = pt.Id,
                                Name = pt.Name ?? $"Loại #{pt.Id}",
                                FinalPrice = PriceCalculator.GetProductTypeFinalPrice(pt),
                                DiscountType = pt.DiscountType,
                                Discount = pt.Discount,
                                IsPublish = pt.IsPublish
                            })
                            .ToList()
                    });
                }

                return Ok(new ComboItemsImportResponse
                {
                    Items = items,
                    Products = products,
                    InvalidEntries = invalidEntries
                });
            }
            catch (Exception)
            {
                return BadRequest(new { message = "File không hợp lệ hoặc bị hỏng." });
            }
        }

        private void ValidateComboRequest(ComboRequest request)
        {
            if (request.DiscountType == DiscountType.FixedAmount)
            {
                ModelState.AddModelError(nameof(request.DiscountType), "Combo không hỗ trợ giá ưu đãi cố định.");
            }

            if (request.DiscountType != DiscountType.None && !request.Discount.HasValue)
            {
                ModelState.AddModelError(nameof(request.Discount), "Vui lòng nhập giá trị giảm giá.");
            }

            if (request.DiscountType == DiscountType.Percent && request.Discount.HasValue)
            {
                if (request.Discount.Value < 0 || request.Discount.Value > 100)
                {
                    ModelState.AddModelError(nameof(request.Discount), "Giá trị giảm giá phải nằm trong khoảng 0 - 100%.");
                }
            }

            if (request.Items == null || !request.Items.Any())
            {
                ModelState.AddModelError(nameof(request.Items), "Combo phải có ít nhất một sản phẩm.");
            }
            else
            {
                for (var index = 0; index < request.Items.Count; index++)
                {
                    var item = request.Items[index];
                    if (item.ProductId == null || item.ProductId <= 0)
                    {
                        ModelState.AddModelError($"Items[{index}].ProductId", "Sản phẩm không hợp lệ.");
                    }

                    if (item.ProductTypeId == null || item.ProductTypeId <= 0)
                    {
                        ModelState.AddModelError($"Items[{index}].ProductTypeId", "Vui lòng chọn loại sản phẩm.");
                    }

                    if (item.Quantity <= 0)
                    {
                        ModelState.AddModelError($"Items[{index}].Quantity", "Số lượng phải lớn hơn 0.");
                    }
                }
            }
        }

        private static long? NormalizeDiscount(ComboRequest request)
        {
            return request.DiscountType switch
            {
                DiscountType.None => null,
                DiscountType.Percent => request.Discount ?? 0,
                _ => request.Discount
            };
        }

        private async Task<(bool Success, List<(Product? Product, ProductType? ProductType, long Quantity)> Items, List<(string Key, string Error)> Errors)> BuildComboItems(List<ComboItemRequest> items)
        {
            var errors = new List<(string Key, string Error)>();

            if (items == null || items.Count == 0)
            {
                errors.Add((nameof(ComboRequest.Items), "Combo phải có ít nhất một sản phẩm."));
                return (false, new List<(Product?, ProductType?, long)>(), errors);
            }

            var productIds = items
                .Where(i => i.ProductId.HasValue && i.ProductId > 0)
                .Select(i => i.ProductId!.Value)
                .Distinct()
                .ToList();

            var products = await _context.Products
                .Where(p => productIds.Contains(p.Id) && !p.IsDeleted)
                .Include(p => p.ProductTypes)
                .ToListAsync();

            foreach (var product in products)
            {
                product.RefreshDerivedFields();
            }

            if (products.Count != productIds.Count)
            {
                errors.Add((nameof(ComboRequest.Items), "Có sản phẩm không hợp lệ hoặc đã bị xóa."));
                return (false, new List<(Product?, ProductType?, long)>(), errors);
            }

            var productLookup = products.ToDictionary(p => p.Id);
            var normalizedItems = new List<(Product?, ProductType?, long)>();

            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                if (!item.ProductId.HasValue || item.ProductId <= 0)
                {
                    continue;
                }

                if (!productLookup.TryGetValue(item.ProductId.Value, out var product))
                {
                    errors.Add(($"Items[{index}].ProductId", "Sản phẩm không hợp lệ."));
                    continue;
                }

                var availableTypes = product.ProductTypes?
                    .Where(pt => !pt.IsDeleted)
                    .ToList() ?? new List<ProductType>();

                if (!item.ProductTypeId.HasValue || item.ProductTypeId <= 0)
                {
                    errors.Add(($"Items[{index}].ProductTypeId", "Vui lòng chọn loại sản phẩm."));
                    continue;
                }

                var selectedType = availableTypes.FirstOrDefault(pt => pt.Id == item.ProductTypeId.Value);
                if (selectedType == null)
                {
                    errors.Add(($"Items[{index}].ProductTypeId", "Loại sản phẩm không hợp lệ."));
                    continue;
                }

                normalizedItems.Add((product, selectedType, item.Quantity));
            }

            var success = errors.Count == 0;
            return (success, normalizedItems, errors);
        }

        private async Task<List<Product>> GetAuthorizedProducts()
        {
            var hasGetProductAll = User.HasPermission("GetProductAll");
            var userId = CurrentUserId;

            IQueryable<Product> query = _context.Products
                .Where(p => !p.IsDeleted)
                .Include(p => p.ProductTypes.Where(pt => !pt.IsDeleted));

            if (!hasGetProductAll)
            {
                query = query.Where(p => p.CreateBy == userId);
            }

            var products = await query.ToListAsync();

            foreach (var product in products)
            {
                product.RefreshDerivedFields();
            }

            return products;
        }

        private static ComboResponse MapToResponse(Combo combo)
        {
            var items = combo.ComboItems?
                .Where(ci => !ci.IsDeleted)
                .Select(ci =>
                {
                    ci.Product?.RefreshDerivedFields();
                    var productType = ci.ProductType;

                    if (productType == null && ci.ProductTypeId.HasValue)
                    {
                        productType = ci.Product?.ProductTypes?.FirstOrDefault(pt => pt.Id == ci.ProductTypeId.Value);
                    }

                    var finalPrice = productType != null
                        ? PriceCalculator.GetProductTypeFinalPrice(productType)
                        : ci.Product != null
                            ? PriceCalculator.GetProductFinalPrice(ci.Product)
                            : 0;

                    return new ComboItemResponse
                    {
                        Id = ci.Id,
                        ProductId = ci.ProductId,
                        ProductName = ci.Product?.Name ?? $"Sản phẩm #{ci.ProductId}",
                        ProductImageUrl = ci.Product?.ProductImageUrl,
                        ProductTypeId = productType?.Id ?? ci.ProductTypeId,
                        ProductTypeName = productType?.Name,
                        ProductTypeFinalPrice = finalPrice,
                        Quantity = ci.Quantity,
                        ProductFinalPrice = finalPrice
                    };
                })
                .OrderBy(ci => ci.ProductName)
                .ToList() ?? new List<ComboItemResponse>();

            return new ComboResponse
            {
                Id = combo.Id,
                Name = combo.Name,
                Description = combo.Description,
                Price = combo.Price,
                FinalPrice = PriceCalculator.GetComboFinalPrice(combo),
                Stock = combo.Stock,
                Index = combo.Index,
                DiscountType = combo.DiscountType,
                Discount = combo.Discount,
                IsPublish = combo.IsPublish,
                ImageUrl = combo.ImageUrl,
                Sold = combo.Sold,
                TotalEvaluate = combo.TotalEvaluate,
                AverageEvaluate = combo.AverageEvaluate,
                CreatedAt = combo.CreatedAt,
                UpdatedAt = combo.UpdatedAt,
                CreatedBy = combo.CreateBy,
                Items = items
            };
        }

        public class ComboItemsImportResponse
        {
            public List<ComboItemImport> Items { get; set; } = new();

            public List<string> InvalidEntries { get; set; } = new();

            public List<ComboItemProduct> Products { get; set; } = new();
        }

        public class ComboItemImport
        {
            public long ProductId { get; set; }

            public long Quantity { get; set; }
        }

        public class ComboItemProduct
        {
            public long Id { get; set; }

            public string Name { get; set; } = string.Empty;

            public double FinalPrice { get; set; }

            public string? ProductImageUrl { get; set; }

            public List<ComboItemProductType> ProductTypes { get; set; } = new();
        }

        public class ComboItemProductType
        {
            public long Id { get; set; }

            public string Name { get; set; } = string.Empty;

            public double FinalPrice { get; set; }

            public DiscountType DiscountType { get; set; }

            public decimal? Discount { get; set; }

            public bool IsPublish { get; set; }
        }

        public class ComboRequest
        {
            [Required]
            [StringLength(500)]
            public string? Name { get; set; }

            [Required]
            [StringLength(10000)]
            public string? Description { get; set; }

            [Required]
            [Url]
            [StringLength(1000)]
            public string? ImageUrl { get; set; }

            [Range(0, long.MaxValue)]
            public long Stock { get; set; }

            [Range(0, long.MaxValue)]
            public long Index { get; set; }

            public DiscountType DiscountType { get; set; }

            [Range(0, long.MaxValue)]
            public long? Discount { get; set; }

            public bool IsPublish { get; set; }

            public List<ComboItemRequest> Items { get; set; } = new();
        }

        public class ComboItemRequest
        {
            [Required]
            public long? ProductId { get; set; }

            [Required]
            public long? ProductTypeId { get; set; }

            [Range(1, long.MaxValue)]
            public long Quantity { get; set; }
        }

        public class ComboResponse
        {
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public double Price { get; set; }
            public double FinalPrice { get; set; }
            public long Stock { get; set; }
            public long Index { get; set; }
            public DiscountType DiscountType { get; set; }
            public long? Discount { get; set; }
            public bool IsPublish { get; set; }
            public string ImageUrl { get; set; } = string.Empty;
            public long Sold { get; set; }
            public long TotalEvaluate { get; set; }
            public double AverageEvaluate { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? UpdatedAt { get; set; }
            public string? CreatedBy { get; set; }
            public List<ComboItemResponse> Items { get; set; } = new();
        }

        public class ComboItemResponse
        {
            public long Id { get; set; }
            public long ProductId { get; set; }
            public string ProductName { get; set; } = string.Empty;
            public string? ProductImageUrl { get; set; }
            public long? ProductTypeId { get; set; }
            public string? ProductTypeName { get; set; }
            public double ProductTypeFinalPrice { get; set; }
            public long Quantity { get; set; }
            public double ProductFinalPrice { get; set; }
        }

        public class BulkDeleteRequest
        {
            public List<long> Ids { get; set; } = new();
        }

        public class BulkPublishRequest
        {
            public List<long> Ids { get; set; } = new();

            public bool IsPublish { get; set; }
        }
    }
}
