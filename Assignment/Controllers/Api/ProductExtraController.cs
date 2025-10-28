using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Security.Claims;
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
    [Route("api/product-extras")]
    [Authorize]
    public class ProductExtraController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthorizationService _authorizationService;

        public ProductExtraController(ApplicationDbContext context, IAuthorizationService authorizationService)
        {
            _context = context;
            _authorizationService = authorizationService;
        }

        private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

        private IQueryable<ProductExtra> BuildQuery(bool tracking = false)
        {
            var query = tracking ? _context.ProductExtras.AsQueryable() : _context.ProductExtras.AsNoTracking();

            return query
                .Include(extra => extra.ProductExtraProducts.Where(link => !link.IsDeleted))
                    .ThenInclude(link => link.Product)
                        .ThenInclude(product => product!.Category)
                .Include(extra => extra.ProductExtraCombos.Where(link => !link.IsDeleted))
                    .ThenInclude(link => link.Combo);
        }

        [HttpGet]
        public async Task<IActionResult> GetProductExtras([FromQuery] string? search = null, [FromQuery] bool? isPublish = null)
        {
            var canGetAll = User.HasPermission("GetProductExtraAll");
            var canGetOwn = User.HasPermission("GetProductExtra");

            if (!canGetAll && !canGetOwn)
            {
                return Forbid();
            }

            IQueryable<ProductExtra> query = BuildQuery()
                .Where(extra => !extra.IsDeleted);

            if (!canGetAll)
            {
                var currentUserId = CurrentUserId;
                query = query.Where(extra => extra.CreateBy == currentUserId);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                var like = $"%{term}%";
                query = query.Where(extra =>
                    EF.Functions.Like(extra.Name, like) ||
                    EF.Functions.Like(extra.Ingredients, like));
            }

            if (isPublish.HasValue)
            {
                query = query.Where(extra => extra.IsPublish == isPublish.Value);
            }

            var extras = await query
                .OrderByDescending(extra => extra.CreatedAt)
                .ThenBy(extra => extra.Name)
                .ToListAsync();

            var response = extras
                .Select(MapToListItem)
                .ToList();

            return Ok(response);
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetProductExtra(long id)
        {
            var extra = await BuildQuery()
                .FirstOrDefaultAsync(extra => extra.Id == id && !extra.IsDeleted);

            if (extra == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, extra, "GetProductExtraPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            return Ok(MapToDetail(extra));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CreateProductExtraPolicy")]
        public async Task<IActionResult> CreateProductExtra([FromBody] ProductExtraRequest request)
        {
            await ValidateRequestAsync(request);
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var now = DateTime.Now;
            var extra = new ProductExtra
            {
                Name = request.Name!.Trim(),
                ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim(),
                Price = NormalizePrice(request.Price),
                Stock = NormalizeInt(request.Stock),
                DiscountType = request.DiscountType,
                Discount = NormalizeDiscountValue(request.DiscountType, request.Discount),
                Calories = NormalizeInt(request.Calories),
                Ingredients = request.Ingredients!.Trim(),
                IsSpicy = request.IsSpicy,
                IsVegetarian = request.IsVegetarian,
                IsPublish = request.IsPublish,
                CreateBy = CurrentUserId,
                CreatedAt = now,
                UpdatedAt = null,
                DeletedAt = null,
                IsDeleted = false
            };

            _context.ProductExtras.Add(extra);
            await _context.SaveChangesAsync();

            await UpdateAssociationsAsync(extra, request.ApplicableProductIds, request.ApplicableComboIds, now);
            await _context.SaveChangesAsync();

            var created = await BuildQuery()
                .FirstOrDefaultAsync(e => e.Id == extra.Id);

            return CreatedAtAction(nameof(GetProductExtra), new { id = extra.Id }, MapToDetail(created ?? extra));
        }

        [HttpPut("{id:long}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProductExtra(long id, [FromBody] ProductExtraRequest request)
        {
            var extra = await BuildQuery(tracking: true)
                .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
            if (extra == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, extra, "UpdateProductExtraPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            await ValidateRequestAsync(request);
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var now = DateTime.Now;
            extra.Name = request.Name!.Trim();
            extra.ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim();
            extra.Price = NormalizePrice(request.Price);
            extra.Stock = NormalizeInt(request.Stock);
            extra.DiscountType = request.DiscountType;
            extra.Discount = NormalizeDiscountValue(request.DiscountType, request.Discount);
            extra.Calories = NormalizeInt(request.Calories);
            extra.Ingredients = request.Ingredients!.Trim();
            extra.IsSpicy = request.IsSpicy;
            extra.IsVegetarian = request.IsVegetarian;
            extra.IsPublish = request.IsPublish;
            extra.UpdatedAt = now;

            await UpdateAssociationsAsync(extra, request.ApplicableProductIds, request.ApplicableComboIds, now);

            await _context.SaveChangesAsync();

            return Ok(MapToDetail(extra));
        }

        [HttpDelete("{id:long}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProductExtra(long id)
        {
            var extra = await BuildQuery(tracking: true)
                .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
            if (extra == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, extra, "DeleteProductExtraPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            var now = DateTime.Now;
            extra.IsDeleted = true;
            extra.DeletedAt = now;
            extra.UpdatedAt = now;

            if (extra.ProductExtraProducts != null)
            {
                foreach (var link in extra.ProductExtraProducts.Where(link => !link.IsDeleted))
                {
                    link.IsDeleted = true;
                    link.DeletedAt = now;
                    link.UpdatedAt = now;
                }
            }

            if (extra.ProductExtraCombos != null)
            {
                foreach (var link in extra.ProductExtraCombos.Where(link => !link.IsDeleted))
                {
                    link.IsDeleted = true;
                    link.DeletedAt = now;
                    link.UpdatedAt = now;
                }
            }

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("bulk-delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteRequest request)
        {
            if (request?.Ids == null || request.Ids.Count == 0)
            {
                return BadRequest(new { message = "Vui lòng chọn ít nhất một sản phẩm bổ sung để xóa." });
            }

            var extras = await BuildQuery(tracking: true)
                .Where(e => request.Ids.Contains(e.Id) && !e.IsDeleted)
                .ToListAsync();

            if (extras.Count == 0)
            {
                return Ok(new { deleted = 0, unauthorized = request.Ids.Count, blocked = 0 });
            }

            var now = DateTime.Now;
            var deleted = 0;
            var unauthorized = 0;

            foreach (var extra in extras)
            {
                var authResult = await _authorizationService.AuthorizeAsync(User, extra, "DeleteProductExtraPolicy");
                if (!authResult.Succeeded)
                {
                    unauthorized++;
                    continue;
                }

                extra.IsDeleted = true;
                extra.DeletedAt = now;
                extra.UpdatedAt = now;

                if (extra.ProductExtraProducts != null)
                {
                    foreach (var link in extra.ProductExtraProducts.Where(link => !link.IsDeleted))
                    {
                        link.IsDeleted = true;
                        link.DeletedAt = now;
                        link.UpdatedAt = now;
                    }
                }

                if (extra.ProductExtraCombos != null)
                {
                    foreach (var link in extra.ProductExtraCombos.Where(link => !link.IsDeleted))
                    {
                        link.IsDeleted = true;
                        link.DeletedAt = now;
                        link.UpdatedAt = now;
                    }
                }

                deleted++;
            }

            if (deleted > 0)
            {
                await _context.SaveChangesAsync();
            }

            return Ok(new { deleted, unauthorized, blocked = 0 });
        }

        [HttpGet("template")]
        public IActionResult DownloadTemplate()
        {
            if (!User.HasAnyPermission("CreateProductExtra", "UpdateProductExtra", "UpdateProductExtraAll"))
            {
                return Forbid();
            }

            using var workbook = new XLWorkbook();
            var productSheet = workbook.Worksheets.Add("ApplicableProducts");
            productSheet.Cell(1, 1).Value = "ProductId";
            productSheet.Cell(2, 1).Value = "1";
            productSheet.Cell(3, 1).Value = "2";

            var comboSheet = workbook.Worksheets.Add("ApplicableCombos");
            comboSheet.Cell(1, 1).Value = "ComboId";
            comboSheet.Cell(2, 1).Value = "1";
            comboSheet.Cell(3, 1).Value = "2";

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            var content = stream.ToArray();
            const string fileName = "product_extra_products_template.xlsx";
            const string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            return File(content, contentType, fileName);
        }

        [HttpPost("import-products")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportApplicableProducts([FromForm] IFormFile? file)
        {
            if (!User.HasAnyPermission("CreateProductExtra", "UpdateProductExtra", "UpdateProductExtraAll"))
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

                var orderedIds = new List<long>();
                var seenIds = new HashSet<long>();
                var invalidEntries = new List<string>();

                foreach (var row in worksheet.RowsUsed())
                {
                    var rawValue = row.Cell(1).GetString()?.Trim();
                    if (string.IsNullOrWhiteSpace(rawValue))
                    {
                        continue;
                    }

                    if (row.RowNumber() == 1 && string.Equals(rawValue, "ProductId", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!long.TryParse(rawValue, out var productId) || productId <= 0)
                    {
                        invalidEntries.Add(rawValue);
                        continue;
                    }

                    if (seenIds.Add(productId))
                    {
                        orderedIds.Add(productId);
                    }
                }

                if (orderedIds.Count == 0)
                {
                    return Ok(new ProductExtraImportResponse
                    {
                        InvalidEntries = invalidEntries
                    });
                }

                var accessibleIds = await GetAccessibleProductIdsAsync(orderedIds);
                var accessibleSet = accessibleIds.ToHashSet();

                foreach (var id in orderedIds)
                {
                    if (!accessibleSet.Contains(id))
                    {
                        invalidEntries.Add(id.ToString());
                    }
                }

                var allowedIds = orderedIds
                    .Where(id => accessibleSet.Contains(id))
                    .ToList();

                if (allowedIds.Count == 0)
                {
                    return Ok(new ProductExtraImportResponse
                    {
                        InvalidEntries = invalidEntries
                    });
                }

                var options = await GetProductOptionsByIdsAsync(allowedIds);
                var lookup = options.ToDictionary(option => option.Id, option => option);
                var orderedOptions = allowedIds
                    .Where(id => lookup.ContainsKey(id))
                    .Select(id => lookup[id])
                    .ToList();

                return Ok(new ProductExtraImportResponse
                {
                    Products = orderedOptions,
                    InvalidEntries = invalidEntries
                });
            }
            catch (Exception)
            {
                return BadRequest(new { message = "File không hợp lệ hoặc bị hỏng." });
            }
        }

        [HttpPost("import-combos")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportApplicableCombos([FromForm] IFormFile? file)
        {
            if (!User.HasAnyPermission("CreateProductExtra", "UpdateProductExtra", "UpdateProductExtraAll"))
            {
                return Forbid();
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "Vui lòng chọn file chứa danh sách combo." });
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
                    return BadRequest(new { message = "File không chứa dữ liệu combo." });
                }

                var orderedIds = new List<long>();
                var seenIds = new HashSet<long>();
                var invalidEntries = new List<string>();

                foreach (var row in worksheet.RowsUsed())
                {
                    var rawValue = row.Cell(1).GetString()?.Trim();
                    if (string.IsNullOrWhiteSpace(rawValue))
                    {
                        continue;
                    }

                    if (row.RowNumber() == 1 && string.Equals(rawValue, "ComboId", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!long.TryParse(rawValue, out var comboId) || comboId <= 0)
                    {
                        invalidEntries.Add(rawValue);
                        continue;
                    }

                    if (seenIds.Add(comboId))
                    {
                        orderedIds.Add(comboId);
                    }
                }

                if (orderedIds.Count == 0)
                {
                    return Ok(new ProductExtraImportResponse
                    {
                        InvalidEntries = invalidEntries
                    });
                }

                var accessibleIds = await GetAccessibleComboIdsAsync(orderedIds);
                var accessibleSet = accessibleIds.ToHashSet();

                foreach (var id in orderedIds)
                {
                    if (!accessibleSet.Contains(id))
                    {
                        invalidEntries.Add(id.ToString());
                    }
                }

                var allowedIds = orderedIds
                    .Where(id => accessibleSet.Contains(id))
                    .ToList();

                if (allowedIds.Count == 0)
                {
                    return Ok(new ProductExtraImportResponse
                    {
                        InvalidEntries = invalidEntries
                    });
                }

                var options = await GetComboOptionsByIdsAsync(allowedIds);
                var lookup = options.ToDictionary(option => option.Id, option => option);
                var orderedOptions = allowedIds
                    .Where(id => lookup.ContainsKey(id))
                    .Select(id => lookup[id])
                    .ToList();

                return Ok(new ProductExtraImportResponse
                {
                    Combos = orderedOptions,
                    InvalidEntries = invalidEntries
                });
            }
            catch (Exception)
            {
                return BadRequest(new { message = "File không hợp lệ hoặc bị hỏng." });
            }
        }

        [HttpGet("product-options")]
        public async Task<IActionResult> GetProductOptions([FromQuery] string? search = null)
        {
            var canGetAllProducts = User.HasPermission("GetProductAll");
            var canGetOwnProducts = User.HasPermission("GetProduct");

            if (!canGetAllProducts && !canGetOwnProducts)
            {
                return Forbid();
            }

            IQueryable<Product> query = _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Where(p => !p.IsDeleted);

            if (!canGetAllProducts)
            {
                var currentUserId = CurrentUserId;
                query = query.Where(p => p.CreateBy == currentUserId);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                var like = $"%{term}%";
                query = query.Where(p => EF.Functions.Like(p.Name, like));
            }

            var products = await query
                .OrderBy(p => p.Name)
                .Take(50)
                .Select(p => new ProductOption
                {
                    Id = p.Id,
                    Name = p.Name,
                    CategoryName = p.Category != null ? p.Category.Name : null,
                    IsPublish = p.IsPublish
                })
                .ToListAsync();

            return Ok(products);
        }

        [HttpGet("combo-options")]
        public async Task<IActionResult> GetComboOptions([FromQuery] string? search = null)
        {
            var canGetAllCombos = User.HasPermission("GetComboAll");
            var canGetOwnCombos = User.HasPermission("GetCombo");

            if (!canGetAllCombos && !canGetOwnCombos)
            {
                return Forbid();
            }

            IQueryable<Combo> query = _context.Combos
                .AsNoTracking()
                .Where(c => !c.IsDeleted);

            if (!canGetAllCombos)
            {
                var currentUserId = CurrentUserId;
                query = query.Where(c => c.CreateBy == currentUserId);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                var like = $"%{term}%";
                query = query.Where(c => EF.Functions.Like(c.Name, like));
            }

            var combos = await query
                .OrderBy(c => c.Name)
                .Take(50)
                .Select(c => new ComboOption
                {
                    Id = c.Id,
                    Name = c.Name,
                    IsPublish = c.IsPublish
                })
                .ToListAsync();

            return Ok(combos);
        }

        private async Task ValidateRequestAsync(ProductExtraRequest request)
        {
            request.ApplicableProductIds ??= new List<long>();
            request.ApplicableComboIds ??= new List<long>();

            if (!ModelState.IsValid)
            {
                return;
            }

            if (request.DiscountType == DiscountType.None)
            {
                request.Discount = null;
            }
            else if (!request.Discount.HasValue)
            {
                ModelState.AddModelError(nameof(ProductExtraRequest.Discount), "Vui lòng nhập giá trị giảm giá.");
            }
            else if (request.DiscountType == DiscountType.Percent)
            {
                if (request.Discount < 0 || request.Discount > 100)
                {
                    ModelState.AddModelError(nameof(ProductExtraRequest.Discount), "Giá trị phần trăm giảm giá phải nằm trong khoảng 0-100.");
                }
            }
            else if (request.Discount < 0)
            {
                ModelState.AddModelError(nameof(ProductExtraRequest.Discount), "Giá trị giảm giá không hợp lệ.");
            }

            if (request.ApplicableProductIds != null && request.ApplicableProductIds.Count > 0)
            {
                var distinctIds = request.ApplicableProductIds
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList();

                if (distinctIds.Count == 0)
                {
                    request.ApplicableProductIds.Clear();
                    return;
                }

                var accessibleProductIds = await GetAccessibleProductIdsAsync(distinctIds);
                if (accessibleProductIds.Count != distinctIds.Count)
                {
                    ModelState.AddModelError(nameof(ProductExtraRequest.ApplicableProductIds), "Một số sản phẩm không hợp lệ hoặc bạn không có quyền truy cập.");
                }

                request.ApplicableProductIds = accessibleProductIds.ToList();
            }

            if (request.ApplicableComboIds != null && request.ApplicableComboIds.Count > 0)
            {
                var distinctComboIds = request.ApplicableComboIds
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList();

                if (distinctComboIds.Count == 0)
                {
                    request.ApplicableComboIds.Clear();
                    return;
                }

                var accessibleComboIds = await GetAccessibleComboIdsAsync(distinctComboIds);
                if (accessibleComboIds.Count != distinctComboIds.Count)
                {
                    ModelState.AddModelError(nameof(ProductExtraRequest.ApplicableComboIds), "Một số combo không hợp lệ hoặc bạn không có quyền truy cập.");
                }

                request.ApplicableComboIds = accessibleComboIds.ToList();
            }
        }

        private async Task<HashSet<long>> GetAccessibleProductIdsAsync(IReadOnlyCollection<long> ids)
        {
            if (ids.Count == 0)
            {
                return new HashSet<long>();
            }

            var canGetAllProducts = User.HasPermission("GetProductAll");
            var canGetOwnProducts = User.HasPermission("GetProduct");

            if (!canGetAllProducts && !canGetOwnProducts)
            {
                ModelState.AddModelError(nameof(ProductExtraRequest.ApplicableProductIds), "Bạn không có quyền chọn sản phẩm áp dụng.");
                return new HashSet<long>();
            }

            IQueryable<Product> query = _context.Products
                .AsNoTracking()
                .Where(p => !p.IsDeleted && ids.Contains(p.Id));

            if (!canGetAllProducts)
            {
                var currentUserId = CurrentUserId;
                query = query.Where(p => p.CreateBy == currentUserId);
            }

            var accessibleIds = await query
                .Select(p => p.Id)
                .ToListAsync();

            return accessibleIds.ToHashSet();
        }

        private async Task<HashSet<long>> GetAccessibleComboIdsAsync(IReadOnlyCollection<long> ids)
        {
            if (ids.Count == 0)
            {
                return new HashSet<long>();
            }

            var canGetAllCombos = User.HasPermission("GetComboAll");
            var canGetOwnCombos = User.HasPermission("GetCombo");

            if (!canGetAllCombos && !canGetOwnCombos)
            {
                ModelState.AddModelError(nameof(ProductExtraRequest.ApplicableComboIds), "Bạn không có quyền chọn combo áp dụng.");
                return new HashSet<long>();
            }

            IQueryable<Combo> query = _context.Combos
                .AsNoTracking()
                .Where(c => !c.IsDeleted && ids.Contains(c.Id));

            if (!canGetAllCombos)
            {
                var currentUserId = CurrentUserId;
                query = query.Where(c => c.CreateBy == currentUserId);
            }

            var accessibleIds = await query
                .Select(c => c.Id)
                .ToListAsync();

            return accessibleIds.ToHashSet();
        }

        private async Task<List<ProductOption>> GetProductOptionsByIdsAsync(IReadOnlyCollection<long> ids)
        {
            if (ids == null || ids.Count == 0)
            {
                return new List<ProductOption>();
            }

            var canGetAllProducts = User.HasPermission("GetProductAll");
            var canGetOwnProducts = User.HasPermission("GetProduct");

            if (!canGetAllProducts && !canGetOwnProducts)
            {
                return new List<ProductOption>();
            }

            IQueryable<Product> query = _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Where(p => !p.IsDeleted && ids.Contains(p.Id));

            if (!canGetAllProducts)
            {
                var currentUserId = CurrentUserId;
                query = query.Where(p => p.CreateBy == currentUserId);
            }

            var products = await query
                .Select(p => new ProductOption
                {
                    Id = p.Id,
                    Name = p.Name,
                    CategoryName = p.Category != null ? p.Category.Name : null,
                    IsPublish = p.IsPublish
                })
                .ToListAsync();

            return products;
        }

        private async Task<List<ComboOption>> GetComboOptionsByIdsAsync(IReadOnlyCollection<long> ids)
        {
            if (ids == null || ids.Count == 0)
            {
                return new List<ComboOption>();
            }

            var canGetAllCombos = User.HasPermission("GetComboAll");
            var canGetOwnCombos = User.HasPermission("GetCombo");

            if (!canGetAllCombos && !canGetOwnCombos)
            {
                return new List<ComboOption>();
            }

            IQueryable<Combo> query = _context.Combos
                .AsNoTracking()
                .Where(c => !c.IsDeleted && ids.Contains(c.Id));

            if (!canGetAllCombos)
            {
                var currentUserId = CurrentUserId;
                query = query.Where(c => c.CreateBy == currentUserId);
            }

            var combos = await query
                .Select(c => new ComboOption
                {
                    Id = c.Id,
                    Name = c.Name,
                    IsPublish = c.IsPublish
                })
                .ToListAsync();

            return combos;
        }

        private async Task UpdateAssociationsAsync(ProductExtra extra, IList<long>? productIds, IList<long>? comboIds, DateTime now)
        {
            await UpdateProductAssociationsAsync(extra, productIds, now);
            await UpdateComboAssociationsAsync(extra, comboIds, now);
        }

        private Task UpdateProductAssociationsAsync(ProductExtra extra, IList<long>? requestedIds, DateTime now)
        {
            extra.ProductExtraProducts ??= new List<ProductExtraProduct>();

            var validIds = requestedIds?.Where(id => id > 0).Distinct().ToList() ?? new List<long>();

            var existing = extra.ProductExtraProducts
                .Where(link => !link.IsDeleted)
                .ToList();

            var existingByProductId = existing.ToDictionary(link => link.ProductId, link => link);

            foreach (var link in existing)
            {
                if (!validIds.Contains(link.ProductId))
                {
                    link.IsDeleted = true;
                    link.DeletedAt = now;
                    link.UpdatedAt = now;
                }
            }

            foreach (var productId in validIds)
            {
                if (existingByProductId.TryGetValue(productId, out var link))
                {
                    link.UpdatedAt = now;
                    link.IsDeleted = false;
                    link.DeletedAt = null;
                    continue;
                }

                extra.ProductExtraProducts.Add(new ProductExtraProduct
                {
                    ProductExtraId = extra.Id,
                    ProductId = productId,
                    CreateBy = CurrentUserId,
                    CreatedAt = now,
                    UpdatedAt = null,
                    DeletedAt = null,
                    IsDeleted = false
                });
            }

            return Task.CompletedTask;
        }

        private Task UpdateComboAssociationsAsync(ProductExtra extra, IList<long>? requestedIds, DateTime now)
        {
            extra.ProductExtraCombos ??= new List<ProductExtraCombo>();

            var validIds = requestedIds?.Where(id => id > 0).Distinct().ToList() ?? new List<long>();

            var existing = extra.ProductExtraCombos
                .Where(link => !link.IsDeleted)
                .ToList();

            var existingByComboId = existing.ToDictionary(link => link.ComboId, link => link);

            foreach (var link in existing)
            {
                if (!validIds.Contains(link.ComboId))
                {
                    link.IsDeleted = true;
                    link.DeletedAt = now;
                    link.UpdatedAt = now;
                }
            }

            foreach (var comboId in validIds)
            {
                if (existingByComboId.TryGetValue(comboId, out var link))
                {
                    link.UpdatedAt = now;
                    link.IsDeleted = false;
                    link.DeletedAt = null;
                    continue;
                }

                extra.ProductExtraCombos.Add(new ProductExtraCombo
                {
                    ProductExtraId = extra.Id,
                    ComboId = comboId,
                    CreateBy = CurrentUserId,
                    CreatedAt = now,
                    UpdatedAt = null,
                    DeletedAt = null,
                    IsDeleted = false
                });
            }

            return Task.CompletedTask;
        }

        private static decimal NormalizePrice(decimal price)
        {
            if (price < 0)
            {
                return 0;
            }

            return Math.Round(price, 2);
        }

        private static int NormalizeInt(int value)
        {
            if (value < 0)
            {
                return 0;
            }

            return value;
        }

        private static decimal? NormalizeDiscountValue(DiscountType type, decimal? value)
        {
            if (type == DiscountType.None || !value.HasValue)
            {
                return null;
            }

            var normalized = value.Value < 0 ? 0 : value.Value;
            return Math.Round(normalized, 2);
        }

        private static ProductExtraListItem MapToListItem(ProductExtra extra)
        {
            return new ProductExtraListItem
            {
                Id = extra.Id,
                Name = extra.Name,
                ImageUrl = extra.ImageUrl,
                Price = extra.Price,
                DiscountType = extra.DiscountType,
                Discount = extra.Discount,
                Stock = extra.Stock,
                Calories = extra.Calories,
                IsSpicy = extra.IsSpicy,
                IsVegetarian = extra.IsVegetarian,
                IsPublish = extra.IsPublish,
                Ingredients = extra.Ingredients,
                CreatedAt = extra.CreatedAt,
                UpdatedAt = extra.UpdatedAt,
                ApplicableProducts = extra.ProductExtraProducts?
                    .Where(link => !link.IsDeleted && link.Product != null)
                    .Select(link => new ProductReference
                    {
                        Id = link.ProductId,
                        Name = link.Product!.Name,
                        CategoryName = link.Product.Category?.Name
                    })
                    .OrderBy(p => p.Name)
                    .ToList() ?? new List<ProductReference>(),
                ApplicableCombos = extra.ProductExtraCombos?
                    .Where(link => !link.IsDeleted && link.Combo != null)
                    .Select(link => new ComboReference
                    {
                        Id = link.ComboId,
                        Name = link.Combo!.Name
                    })
                    .OrderBy(c => c.Name)
                    .ToList() ?? new List<ComboReference>()
            };
        }

        private static ProductExtraDetail MapToDetail(ProductExtra extra)
        {
            return new ProductExtraDetail
            {
                Id = extra.Id,
                Name = extra.Name,
                ImageUrl = extra.ImageUrl,
                Price = extra.Price,
                DiscountType = extra.DiscountType,
                Discount = extra.Discount,
                Stock = extra.Stock,
                Calories = extra.Calories,
                Ingredients = extra.Ingredients,
                IsSpicy = extra.IsSpicy,
                IsVegetarian = extra.IsVegetarian,
                IsPublish = extra.IsPublish,
                CreatedAt = extra.CreatedAt,
                UpdatedAt = extra.UpdatedAt,
                CreatedBy = extra.CreateBy,
                ApplicableProducts = extra.ProductExtraProducts?
                    .Where(link => !link.IsDeleted && link.Product != null)
                    .Select(link => new ProductReference
                    {
                        Id = link.ProductId,
                        Name = link.Product!.Name,
                        CategoryName = link.Product.Category?.Name
                    })
                    .OrderBy(p => p.Name)
                    .ToList() ?? new List<ProductReference>(),
                ApplicableCombos = extra.ProductExtraCombos?
                    .Where(link => !link.IsDeleted && link.Combo != null)
                    .Select(link => new ComboReference
                    {
                        Id = link.ComboId,
                        Name = link.Combo!.Name
                    })
                    .OrderBy(c => c.Name)
                    .ToList() ?? new List<ComboReference>()
            };
        }

        public class ProductExtraRequest
        {
            [Required]
            [StringLength(200)]
            public string? Name { get; set; }

            [StringLength(1000)]
            [DataType(DataType.Url)]
            public string? ImageUrl { get; set; }

            [Range(typeof(decimal), "0", "79228162514264337593543950335")]
            public decimal Price { get; set; }

            [Range(0, int.MaxValue)]
            public int Stock { get; set; }

            [Required]
            public DiscountType DiscountType { get; set; }

            public decimal? Discount { get; set; }

            [Range(0, int.MaxValue)]
            public int Calories { get; set; }

            [Required]
            [StringLength(2000)]
            public string? Ingredients { get; set; }

            public bool IsSpicy { get; set; }

            public bool IsVegetarian { get; set; }

            public bool IsPublish { get; set; }

            public List<long> ApplicableProductIds { get; set; } = new();
            public List<long> ApplicableComboIds { get; set; } = new();
        }

        public class ProductExtraListItem
        {
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? ImageUrl { get; set; }
            public decimal Price { get; set; }
            public DiscountType DiscountType { get; set; }
            public decimal? Discount { get; set; }
            public int Stock { get; set; }
            public int Calories { get; set; }
            public bool IsSpicy { get; set; }
            public bool IsVegetarian { get; set; }
            public bool IsPublish { get; set; }
            public string Ingredients { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public DateTime? UpdatedAt { get; set; }
            public List<ProductReference> ApplicableProducts { get; set; } = new();
            public List<ComboReference> ApplicableCombos { get; set; } = new();
        }

        public class ProductExtraDetail : ProductExtraListItem
        {
            public string? CreatedBy { get; set; }
        }

        public class ProductReference
        {
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? CategoryName { get; set; }
        }

        public class ComboReference
        {
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public class ProductExtraImportResponse
        {
            public List<ProductOption> Products { get; set; } = new();
            public List<ComboOption> Combos { get; set; } = new();

            public List<string> InvalidEntries { get; set; } = new();
        }

        public class ProductOption
        {
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? CategoryName { get; set; }
            public bool IsPublish { get; set; }
        }

        public class ComboOption
        {
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public bool IsPublish { get; set; }
        }

        public class BulkDeleteRequest
        {
            public List<long> Ids { get; set; } = new();
        }
    }
}
