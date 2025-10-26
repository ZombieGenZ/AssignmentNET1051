using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
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
    [Route("api/[controller]s")]
    [Authorize]
    public class ProductController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthorizationService _authorizationService;

        public ProductController(ApplicationDbContext context, IAuthorizationService authorizationService)
        {
            _context = context;
            _authorizationService = authorizationService;
        }

        private string? CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        [HttpGet]
        public async Task<IActionResult> GetProducts()
        {
            var canGetAll = User.HasPermission("GetProductAll");
            var canGetOwn = User.HasPermission("GetProduct");

            if (!canGetAll && !canGetOwn)
            {
                return Forbid();
            }

            IQueryable<Product> query = _context.Products
                .AsNoTracking()
                .Include(p => p.ProductTypes)
                .Include(p => p.Category)
                .Where(p => !p.IsDeleted);

            if (!canGetAll)
            {
                query = query.Where(p => p.CreateBy == CurrentUserId);
            }

            var products = await query
                .OrderByDescending(p => p.CreatedAt)
                .ThenBy(p => p.Name)
                .ToListAsync();

            foreach (var product in products)
            {
                product.RefreshDerivedFields();
            }

            return Ok(products.Select(MapToResponse));
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetProduct(long id)
        {
            var product = await _context.Products
                .Include(p => p.ProductTypes)
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

            if (product == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, product, "GetProductPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            product.RefreshDerivedFields();

            return Ok(MapToResponse(product));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CreateProductPolicy")]
        public async Task<IActionResult> CreateProduct([FromBody] ProductRequest request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var product = new Product
            {
                TotalEvaluate = 0,
                AverageEvaluate = 0,
                CreateBy = CurrentUserId,
                CreatedAt = DateTime.Now,
                UpdatedAt = null,
                DeletedAt = null,
                IsDeleted = false
            };

            if (!TryApplyRequestToProduct(product, request, CurrentUserId, out var applyError))
            {
                ModelState.AddModelError(nameof(ProductRequest.ProductTypes), applyError ?? "Vui lòng cung cấp ít nhất một loại sản phẩm hợp lệ.");
                return ValidationProblem(ModelState);
            }
            product.RefreshDerivedFields();

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            await UpdateRelatedComboPrices(product.Id);

            var response = MapToResponse(product);
            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, response);
        }

        [HttpPut("{id:long}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProduct(long id, [FromBody] ProductRequest request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var product = await _context.Products
                .Include(p => p.ProductTypes)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (product == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, product, "UpdateProductPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            if (!TryApplyRequestToProduct(product, request, CurrentUserId, out var applyError))
            {
                ModelState.AddModelError(nameof(ProductRequest.ProductTypes), applyError ?? "Vui lòng cung cấp ít nhất một loại sản phẩm hợp lệ.");
                return ValidationProblem(ModelState);
            }
            product.RefreshDerivedFields();
            product.UpdatedAt = DateTime.Now;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await ProductExists(id))
                {
                    return NotFound();
                }

                throw;
            }

            await UpdateRelatedComboPrices(product.Id);

            return Ok(MapToResponse(product));
        }

        [HttpDelete("{id:long}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(long id)
        {
            var product = await _context.Products
                .Include(p => p.ProductTypes)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (product == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, product, "DeleteProductPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            var comboLookup = await GetActiveComboNamesByProductIds(new[] { product.Id });
            if (comboLookup.TryGetValue(product.Id, out var combos) && combos.Any())
            {
                return BadRequest(new
                {
                    message = BuildComboDependencyMessage(product.Name, combos),
                    combos
                });
            }

            product.IsDeleted = true;
            product.DeletedAt = DateTime.Now;
            product.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            await UpdateRelatedComboPrices(product.Id);

            return NoContent();
        }

        [HttpPost("bulk-delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteRequest request)
        {
            if (request.Ids == null || request.Ids.Count == 0)
            {
                ModelState.AddModelError(nameof(request.Ids), "Vui lòng chọn ít nhất một sản phẩm để xóa.");
                return ValidationProblem(ModelState);
            }

            var products = await _context.Products
                .Where(p => request.Ids.Contains(p.Id) && !p.IsDeleted)
                .Include(p => p.ProductTypes)
                .ToListAsync();

            if (!products.Any())
            {
                return NotFound(new { message = "Không tìm thấy sản phẩm hợp lệ để xóa." });
            }

            var productIds = products.Select(p => p.Id).ToList();
            var comboLookup = await GetActiveComboNamesByProductIds(productIds);
            var now = DateTime.Now;
            var deletedCount = 0;
            var unauthorizedCount = 0;
            var affectedProductIds = new HashSet<long>();
            var blockedProducts = new List<object>();

            foreach (var product in products)
            {
                var authResult = await _authorizationService.AuthorizeAsync(User, product, "DeleteProductPolicy");
                if (!authResult.Succeeded)
                {
                    unauthorizedCount++;
                    continue;
                }

                if (comboLookup.TryGetValue(product.Id, out var combos) && combos.Any())
                {
                    blockedProducts.Add(new
                    {
                        productId = product.Id,
                        productName = product.Name,
                        combos
                    });
                    continue;
                }

                product.IsDeleted = true;
                product.DeletedAt = now;
                product.UpdatedAt = now;
                deletedCount++;
                affectedProductIds.Add(product.Id);
            }

            if (deletedCount > 0)
            {
                await _context.SaveChangesAsync();

                foreach (var productId in affectedProductIds)
                {
                    await UpdateRelatedComboPrices(productId);
                }
            }

            if (deletedCount == 0 && unauthorizedCount > 0)
            {
                return Forbid();
            }

            return Ok(new
            {
                deleted = deletedCount,
                unauthorized = unauthorizedCount,
                blocked = blockedProducts.Count,
                blockedProducts
            });
        }

        [HttpPost("bulk-publish")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkPublish([FromBody] BulkPublishRequest request)
        {
            if (request?.Ids == null || request.Ids.Count == 0)
            {
                ModelState.AddModelError(nameof(request.Ids), "Vui lòng chọn ít nhất một sản phẩm để cập nhật.");
                return ValidationProblem(ModelState);
            }

            var products = await _context.Products
                .Where(p => request.Ids.Contains(p.Id) && !p.IsDeleted)
                .ToListAsync();

            if (!products.Any())
            {
                return NotFound(new { message = "Không tìm thấy sản phẩm hợp lệ để cập nhật." });
            }

            var updatedCount = 0;
            var unauthorizedCount = 0;
            var now = DateTime.Now;

            foreach (var product in products)
            {
                var authResult = await _authorizationService.AuthorizeAsync(User, product, "UpdateProductPolicy");
                if (!authResult.Succeeded)
                {
                    unauthorizedCount++;
                    continue;
                }

                var activeTypes = product.ProductTypes?
                    .Where(pt => !pt.IsDeleted)
                    .ToList() ?? new List<ProductType>();

                var changed = false;

                if (!activeTypes.Any())
                {
                    var desiredPublishState = false;
                    if (product.IsPublish != desiredPublishState)
                    {
                        product.IsPublish = desiredPublishState;
                        product.UpdatedAt = now;
                        updatedCount++;
                    }

                    continue;
                }

                foreach (var type in activeTypes)
                {
                    if (type.IsPublish != request.IsPublish)
                    {
                        type.IsPublish = request.IsPublish;
                        type.UpdatedAt = now;
                        changed = true;
                    }
                }

                var desiredProductPublishState = request.IsPublish && activeTypes.Any(pt => pt.IsPublish);
                if (product.IsPublish != desiredProductPublishState)
                {
                    product.IsPublish = desiredProductPublishState;
                    changed = true;
                }

                if (!changed)
                {
                    continue;
                }

                product.RefreshDerivedFields();
                product.UpdatedAt = now;
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

        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await GetAuthorizedCategories();

            var result = categories
                .OrderBy(c => c.Name)
                .Select(c => new
                {
                    c.Id,
                    c.Name
                });

            return Ok(result);
        }

        [HttpGet("types/template")]
        public IActionResult DownloadProductTypeTemplate()
        {
            if (!User.HasAnyPermission("CreateProduct", "CreateProductAll", "UpdateProduct", "UpdateProductAll"))
            {
                return Forbid();
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("ProductTypes");

            worksheet.Cell(1, 1).Value = "Name";
            worksheet.Cell(1, 2).Value = "ProductTypeImageUrl";
            worksheet.Cell(1, 3).Value = "Price";
            worksheet.Cell(1, 4).Value = "Stock";
            worksheet.Cell(1, 5).Value = "DiscountType";
            worksheet.Cell(1, 6).Value = "Discount";
            worksheet.Cell(1, 7).Value = "PreparationTime";
            worksheet.Cell(1, 8).Value = "Calories";
            worksheet.Cell(1, 9).Value = "Ingredients";
            worksheet.Cell(1, 10).Value = "IsSpicy";
            worksheet.Cell(1, 11).Value = "IsVegetarian";
            worksheet.Cell(1, 12).Value = "IsPublish";

            worksheet.Cell(2, 1).Value = "Kích cỡ nhỏ";
            worksheet.Cell(2, 3).Value = 25000;
            worksheet.Cell(2, 4).Value = 10;
            worksheet.Cell(2, 5).Value = "None";
            worksheet.Cell(2, 7).Value = 5;
            worksheet.Cell(2, 8).Value = 120;
            worksheet.Cell(2, 9).Value = "Gạo, thịt gà, rau";
            worksheet.Cell(2, 10).Value = false;
            worksheet.Cell(2, 11).Value = false;
            worksheet.Cell(2, 12).Value = true;

            worksheet.Cell(3, 1).Value = "Kích cỡ lớn";
            worksheet.Cell(3, 3).Value = 32000;
            worksheet.Cell(3, 4).Value = 5;
            worksheet.Cell(3, 5).Value = "Percent";
            worksheet.Cell(3, 6).Value = 10;
            worksheet.Cell(3, 7).Value = 7;
            worksheet.Cell(3, 8).Value = 150;
            worksheet.Cell(3, 9).Value = "Gạo, thịt gà, rau, trứng";
            worksheet.Cell(3, 10).Value = true;
            worksheet.Cell(3, 11).Value = false;
            worksheet.Cell(3, 12).Value = true;

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            var content = stream.ToArray();
            const string fileName = "product_types_template.xlsx";
            const string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            return File(content, contentType, fileName);
        }

        [HttpPost("types/import")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportProductTypes([FromForm] IFormFile? file)
        {
            if (!User.HasAnyPermission("CreateProduct", "CreateProductAll", "UpdateProduct", "UpdateProductAll"))
            {
                return Forbid();
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "Vui lòng chọn file chứa danh sách loại sản phẩm." });
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
                    return BadRequest(new { message = "File không chứa dữ liệu loại sản phẩm." });
                }

                var importedTypes = new List<ProductTypeRequest>();
                var warnings = new List<string>();

                foreach (var row in worksheet.RowsUsed())
                {
                    if (row.RowNumber() == 1)
                    {
                        continue;
                    }

                    var name = row.Cell(1).GetString()?.Trim();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    var imageUrl = row.Cell(2).GetString()?.Trim();
                    var priceValue = ParseDoubleCell(row.Cell(3));
                    if (priceValue < 0)
                    {
                        priceValue = 0;
                    }

                    var stockValue = ParseIntCell(row.Cell(4));
                    if (stockValue < 0)
                    {
                        stockValue = 0;
                    }

                    var discountType = ParseDiscountTypeCell(row.Cell(5));
                    var rawDiscount = ParseDoubleCell(row.Cell(6));
                    if (rawDiscount < 0)
                    {
                        rawDiscount = 0;
                    }

                    var preparationTime = ParseIntCell(row.Cell(7));
                    if (preparationTime < 0)
                    {
                        preparationTime = 0;
                    }

                    var calories = ParseIntCell(row.Cell(8));
                    if (calories < 0)
                    {
                        calories = 0;
                    }

                    var ingredients = row.Cell(9).GetString()?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(ingredients))
                    {
                        warnings.Add($"Dòng {row.RowNumber()}: chưa nhập thành phần.");
                        continue;
                    }

                    var isSpicy = ParseBoolCell(row.Cell(10));
                    var isVegetarian = ParseBoolCell(row.Cell(11));
                    var isPublish = ParseBoolCell(row.Cell(12), true);

                    importedTypes.Add(new ProductTypeRequest
                    {
                        Name = name,
                        ProductTypeImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl,
                        Price = priceValue,
                        Stock = stockValue,
                        DiscountType = discountType,
                        Discount = discountType == DiscountType.None ? null : (long?)Math.Round(rawDiscount),
                        PreparationTime = preparationTime,
                        Calories = calories,
                        Ingredients = ingredients,
                        IsSpicy = isSpicy,
                        IsVegetarian = isVegetarian,
                        IsPublish = isPublish
                    });
                }

                return Ok(new
                {
                    productTypes = importedTypes,
                    warnings
                });
            }
            catch (Exception)
            {
                return BadRequest(new { message = "File không hợp lệ hoặc bị hỏng." });
            }
        }

        private static bool TryApplyRequestToProduct(
            Product product,
            ProductRequest request,
            string? currentUserId,
            out string? errorMessage)
        {
            var now = DateTime.Now;
            var normalizedName = (request.Name ?? string.Empty).Trim();

            product.ProductTypes ??= new List<ProductType>();

            product.Name = normalizedName;
            product.Description = (request.Description ?? string.Empty).Trim();
            product.ProductImageUrl = (request.ProductImageUrl ?? string.Empty).Trim();
            product.CategoryId = request.CategoryId;

            var normalizedTypes = NormalizeProductTypeRequests(product, request, normalizedName);

            if (!normalizedTypes.Any())
            {
                errorMessage = "Vui lòng cung cấp ít nhất một loại sản phẩm hợp lệ.";
                return false;
            }

            var updatedTypes = new HashSet<ProductType>();

            foreach (var typeRequest in normalizedTypes)
            {
                var productType = FindOrCreateProductType(product, typeRequest.Id, currentUserId, now);

                productType.Name = typeRequest.Name;
                productType.ProductTypeImageUrl = string.IsNullOrWhiteSpace(typeRequest.ProductTypeImageUrl)
                    ? null
                    : typeRequest.ProductTypeImageUrl.Trim();
                productType.Price = NormalizePrice(typeRequest.Price);
                productType.Stock = NormalizeInt(typeRequest.Stock);
                productType.DiscountType = typeRequest.DiscountType;
                productType.Discount = NormalizeDiscountValue(typeRequest.DiscountType, typeRequest.Discount);
                productType.PreparationTime = NormalizeInt(typeRequest.PreparationTime);
                productType.Calories = NormalizeInt(typeRequest.Calories);
                productType.Ingredients = (typeRequest.Ingredients ?? string.Empty).Trim();
                productType.IsSpicy = typeRequest.IsSpicy;
                productType.IsVegetarian = typeRequest.IsVegetarian;
                productType.IsPublish = typeRequest.IsPublish;

                updatedTypes.Add(productType);
            }

            foreach (var existing in product.ProductTypes.Where(pt => !pt.IsDeleted).ToList())
            {
                if (!updatedTypes.Contains(existing))
                {
                    existing.IsDeleted = true;
                    existing.DeletedAt = now;
                    existing.UpdatedAt = now;
                    existing.IsPublish = false;
                }
            }

            var hasPublishedTypes = product.ProductTypes
                .Any(pt => !pt.IsDeleted && pt.IsPublish);
            product.IsPublish = request.IsPublish && hasPublishedTypes;

            errorMessage = null;
            return true;
        }

        private static List<ProductTypeRequest> NormalizeProductTypeRequests(
            Product product,
            ProductRequest request,
            string normalizedProductName)
        {
            var normalized = new List<ProductTypeRequest>();

            if (request.ProductTypes != null)
            {
                foreach (var rawType in request.ProductTypes)
                {
                    if (rawType == null)
                    {
                        continue;
                    }

                    var trimmedName = (rawType.Name ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(trimmedName))
                    {
                        continue;
                    }

                    normalized.Add(new ProductTypeRequest
                    {
                        Id = rawType.Id.HasValue && rawType.Id.Value > 0 ? rawType.Id : null,
                        Name = trimmedName,
                        ProductTypeImageUrl = string.IsNullOrWhiteSpace(rawType.ProductTypeImageUrl)
                            ? null
                            : rawType.ProductTypeImageUrl.Trim(),
                        Price = rawType.Price,
                        Stock = rawType.Stock,
                        DiscountType = rawType.DiscountType,
                        Discount = rawType.Discount,
                        PreparationTime = rawType.PreparationTime,
                        Calories = rawType.Calories,
                        Ingredients = (rawType.Ingredients ?? string.Empty).Trim(),
                        IsSpicy = rawType.IsSpicy,
                        IsVegetarian = rawType.IsVegetarian,
                        IsPublish = rawType.IsPublish
                    });
                }
            }

            if (normalized.Any())
            {
                return normalized;
            }

            if (string.IsNullOrWhiteSpace(normalizedProductName))
            {
                return normalized;
            }

            var fallbackIngredients = (request.Ingredients ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(fallbackIngredients))
            {
                return normalized;
            }

            var firstActiveType = product.ProductTypes
                .FirstOrDefault(pt => !pt.IsDeleted);

            normalized.Add(new ProductTypeRequest
            {
                Id = firstActiveType?.Id > 0 ? firstActiveType.Id : null,
                Name = normalizedProductName,
                ProductTypeImageUrl = string.IsNullOrWhiteSpace(request.ProductImageUrl)
                    ? null
                    : request.ProductImageUrl.Trim(),
                Price = request.Price,
                Stock = NormalizeInt(request.Stock),
                DiscountType = request.DiscountType,
                Discount = request.Discount,
                PreparationTime = NormalizeInt(request.PreparationTime),
                Calories = NormalizeInt(request.Calories),
                Ingredients = fallbackIngredients,
                IsSpicy = request.IsSpicy,
                IsVegetarian = request.IsVegetarian,
                IsPublish = request.IsPublish
            });

            return normalized;
        }

        private static ProductType FindOrCreateProductType(
            Product product,
            long? requestedId,
            string? currentUserId,
            DateTime now)
        {
            ProductType? productType = null;

            if (requestedId.HasValue && requestedId.Value > 0)
            {
                productType = product.ProductTypes
                    .FirstOrDefault(pt => pt.Id == requestedId.Value);
            }

            if (productType == null)
            {
                productType = new ProductType
                {
                    Product = product,
                    CreateBy = currentUserId,
                    CreatedAt = now,
                    IsDeleted = false
                };

                product.ProductTypes.Add(productType);
            }
            else if (productType.IsDeleted)
            {
                productType.IsDeleted = false;
                productType.DeletedAt = null;
                productType.UpdatedAt = now;
            }
            else
            {
                productType.UpdatedAt = now;
            }

            return productType;
        }

        private static decimal NormalizePrice(double price)
        {
            if (double.IsNaN(price) || double.IsInfinity(price))
            {
                return 0;
            }

            var normalized = price < 0 ? 0 : price;
            return Math.Round((decimal)normalized, 2);
        }

        private static int NormalizeInt(long value)
        {
            if (value < 0)
            {
                return 0;
            }

            if (value > int.MaxValue)
            {
                return int.MaxValue;
            }

            return (int)value;
        }

        private static decimal? NormalizeDiscountValue(DiscountType type, long? value)
        {
            if (type == DiscountType.None || !value.HasValue)
            {
                return null;
            }

            var normalized = value.Value < 0 ? 0 : value.Value;
            return Math.Round((decimal)normalized, 2);
        }

        private static double ParseDoubleCell(IXLCell cell)
        {
            if (cell.TryGetValue(out double numericValue))
            {
                return numericValue;
            }

            var text = cell.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }

            if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out numericValue))
            {
                return numericValue;
            }

            if (double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out numericValue))
            {
                return numericValue;
            }

            if (double.TryParse(text, NumberStyles.Any, CultureInfo.GetCultureInfo("vi-VN"), out numericValue))
            {
                return numericValue;
            }

            return 0;
        }

        private static int ParseIntCell(IXLCell cell)
        {
            var numericValue = ParseDoubleCell(cell);
            if (double.IsNaN(numericValue) || double.IsInfinity(numericValue))
            {
                return 0;
            }

            var rounded = (long)Math.Round(numericValue);
            if (rounded < 0)
            {
                return 0;
            }

            if (rounded > int.MaxValue)
            {
                return int.MaxValue;
            }

            return (int)rounded;
        }

        private static bool ParseBoolCell(IXLCell cell, bool defaultValue = false)
        {
            if (cell.TryGetValue(out bool boolValue))
            {
                return boolValue;
            }

            var text = cell.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return defaultValue;
            }

            if (bool.TryParse(text, out boolValue))
            {
                return boolValue;
            }

            if (int.TryParse(text, out var intValue))
            {
                return intValue != 0;
            }

            return defaultValue;
        }

        private static DiscountType ParseDiscountTypeCell(IXLCell cell)
        {
            if (cell.TryGetValue(out double numericValue))
            {
                var intValue = (int)Math.Round(numericValue);
                if (Enum.IsDefined(typeof(DiscountType), intValue))
                {
                    return (DiscountType)intValue;
                }
            }

            var text = cell.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return DiscountType.None;
            }

            if (Enum.TryParse(text, true, out DiscountType parsed))
            {
                return parsed;
            }

            return text.ToLowerInvariant() switch
            {
                "percent" => DiscountType.Percent,
                "fixed" => DiscountType.FixedAmount,
                "fixedamount" => DiscountType.FixedAmount,
                "amount" => DiscountType.Amount,
                _ => DiscountType.None
            };
        }

        private async Task<List<Category>> GetAuthorizedCategories()
        {
            var hasGetAll = User.HasPermission("GetCategoryAll");
            IQueryable<Category> query = _context.Categories.Where(c => !c.IsDeleted);

            if (hasGetAll)
            {
                return await query.AsNoTracking().ToListAsync();
            }

            if (!User.HasPermission("GetCategory"))
            {
                return new List<Category>();
            }

            return await query.AsNoTracking()
                .Where(c => c.CreateBy == CurrentUserId)
                .ToListAsync();
        }

        private async Task<bool> ProductExists(long id)
        {
            return await _context.Products.AnyAsync(p => p.Id == id && !p.IsDeleted);
        }

        private async Task<Dictionary<long, List<string>>> GetActiveComboNamesByProductIds(IReadOnlyCollection<long> productIds)
        {
            if (productIds == null || productIds.Count == 0)
            {
                return new Dictionary<long, List<string>>();
            }

            var activeCombos = await _context.ComboItems
                .Where(ci => !ci.IsDeleted && productIds.Contains(ci.ProductId))
                .Join(
                    _context.Combos.Where(c => !c.IsDeleted),
                    ci => ci.ComboId,
                    c => c.Id,
                    (ci, combo) => new { ci.ProductId, combo.Name })
                .ToListAsync();

            return activeCombos
                .GroupBy(item => item.ProductId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(item => item.Name)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Distinct()
                        .ToList());
        }

        private static string BuildComboDependencyMessage(string productName, IReadOnlyCollection<string> comboNames)
        {
            if (comboNames == null || comboNames.Count == 0)
            {
                return $"Không thể xóa sản phẩm \"{productName}\" vì đang được sử dụng trong combo khác.";
            }

            var formattedNames = comboNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => $"\"{name.Trim()}\"")
                .ToList();

            if (formattedNames.Count == 0)
            {
                return $"Không thể xóa sản phẩm \"{productName}\" vì đang được sử dụng trong combo khác.";
            }

            var preview = string.Join(", ", formattedNames.Take(3));
            if (formattedNames.Count > 3)
            {
                preview += $" và {formattedNames.Count - 3} combo khác";
            }

            var combosLabel = formattedNames.Count > 1 ? "các combo" : "combo";
            return $"Không thể xóa sản phẩm \"{productName}\" vì đang được sử dụng trong {combosLabel} {preview}.";
        }

        private static ProductResponse MapToResponse(Product product)
        {
            product.RefreshDerivedFields();

            var defaultType = product.PrimaryProductType
                ?? product.ProductTypes?.FirstOrDefault(pt => !pt.IsDeleted);

            var discountValue = defaultType?.Discount.HasValue == true
                ? (long?)Math.Round(defaultType.Discount.Value)
                : null;

            return new ProductResponse
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = defaultType != null ? (double)defaultType.Price : 0,
                Stock = defaultType?.Stock ?? 0,
                Sold = product.TotalSold,
                DiscountType = defaultType?.DiscountType ?? DiscountType.None,
                Discount = discountValue,
                IsPublish = product.IsPublish,
                ProductImageUrl = product.ProductImageUrl,
                PreparationTime = defaultType?.PreparationTime ?? 0,
                Calories = defaultType?.Calories ?? 0,
                Ingredients = defaultType?.Ingredients ?? string.Empty,
                IsSpicy = product.IsSpicy,
                IsVegetarian = product.IsVegetarian,
                TotalEvaluate = product.TotalEvaluate,
                AverageEvaluate = product.AverageEvaluate,
                CategoryId = product.CategoryId,
                CategoryName = product.Category?.Name,
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt,
                PriceRange = product.PriceRange,
                TotalStock = product.TotalStock,
                TotalSold = product.TotalSold,
                MinPreparationTime = product.MinPreparationTime,
                MinCalories = product.MinCalories,
                ProductTypes = product.ProductTypes?
                    .Where(pt => !pt.IsDeleted)
                    .Select(pt => new ProductTypeResponse
                    {
                        Id = pt.Id,
                        Name = pt.Name,
                        ProductTypeImageUrl = pt.ProductTypeImageUrl,
                        Price = (double)pt.Price,
                        Stock = pt.Stock,
                        Sold = pt.Sold,
                        DiscountType = pt.DiscountType,
                        Discount = pt.Discount.HasValue ? (long?)Math.Round(pt.Discount.Value) : null,
                        IsPublish = pt.IsPublish,
                        IsSpicy = pt.IsSpicy,
                        IsVegetarian = pt.IsVegetarian,
                        PreparationTime = pt.PreparationTime,
                        Calories = pt.Calories,
                        Ingredients = pt.Ingredients
                    })
                    .ToList() ?? new List<ProductTypeResponse>()
            };
        }

        private async Task UpdateRelatedComboPrices(long productId)
        {
            var combos = await _context.Combos
                .Where(c => !c.IsDeleted && c.ComboItems.Any(ci => ci.ProductId == productId && !ci.IsDeleted))
                .Include(c => c.ComboItems)
                .ToListAsync();

            if (!combos.Any())
            {
                return;
            }

            var productIds = combos
                .SelectMany(c => c.ComboItems.Where(ci => !ci.IsDeleted).Select(ci => ci.ProductId))
                .Distinct()
                .ToList();

            var products = await _context.Products
                .Where(p => productIds.Contains(p.Id) && !p.IsDeleted)
                .Include(p => p.ProductTypes)
                .ToListAsync();

            var productLookup = products.ToDictionary(p => p.Id);

            foreach (var combo in combos)
            {
                var priceItems = combo.ComboItems
                    .Where(ci => !ci.IsDeleted)
                    .Select(ci => productLookup.TryGetValue(ci.ProductId, out var product)
                        ? (product, ci.Quantity)
                        : ((Product?)null, ci.Quantity));

                combo.Price = PriceCalculator.GetComboBasePrice(priceItems);
                combo.UpdatedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();
        }

        public class ProductRequest
        {
            [Required]
            [StringLength(500)]
            public string Name { get; set; } = string.Empty;

            [Required]
            [StringLength(10000)]
            public string Description { get; set; } = string.Empty;

            [Range(0, double.MaxValue)]
            public double Price { get; set; }

            [Range(0, long.MaxValue)]
            public long Stock { get; set; }

            [Required]
            public DiscountType DiscountType { get; set; }

            [Range(0, long.MaxValue)]
            public long? Discount { get; set; }

            public bool IsPublish { get; set; }

            [Required]
            [StringLength(1000)]
            [DataType(DataType.Url)]
            public string ProductImageUrl { get; set; } = string.Empty;

            [Range(0, long.MaxValue)]
            public long PreparationTime { get; set; }

            [Range(0, long.MaxValue)]
            public long Calories { get; set; }

            [StringLength(1000)]
            public string Ingredients { get; set; } = string.Empty;

            public bool IsSpicy { get; set; }

            public bool IsVegetarian { get; set; }

            public List<ProductTypeRequest> ProductTypes { get; set; } = new();

            [Range(1, long.MaxValue)]
            public long CategoryId { get; set; }
        }

        public class ProductTypeRequest
        {
            public long? Id { get; set; }

            [Required]
            [StringLength(200)]
            public string Name { get; set; } = string.Empty;

            [StringLength(1000)]
            [DataType(DataType.Url)]
            public string? ProductTypeImageUrl { get; set; }

            [Range(0, double.MaxValue)]
            public double Price { get; set; }

            [Range(0, int.MaxValue)]
            public int Stock { get; set; }

            [Required]
            public DiscountType DiscountType { get; set; } = DiscountType.None;

            [Range(0, long.MaxValue)]
            public long? Discount { get; set; }

            [Range(0, int.MaxValue)]
            public int PreparationTime { get; set; }

            [Range(0, int.MaxValue)]
            public int Calories { get; set; }

            [Required]
            [StringLength(2000)]
            public string Ingredients { get; set; } = string.Empty;

            public bool IsSpicy { get; set; }

            public bool IsVegetarian { get; set; }

            public bool IsPublish { get; set; } = true;
        }

        public class ProductResponse
        {
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public double Price { get; set; }
            public long Stock { get; set; }
            public long Sold { get; set; }
            public DiscountType DiscountType { get; set; }
            public long? Discount { get; set; }
            public bool IsPublish { get; set; }
            public string ProductImageUrl { get; set; } = string.Empty;
            public long PreparationTime { get; set; }
            public long Calories { get; set; }
            public string Ingredients { get; set; } = string.Empty;
            public bool IsSpicy { get; set; }
            public bool IsVegetarian { get; set; }
            public long TotalEvaluate { get; set; }
            public double AverageEvaluate { get; set; }
            public long CategoryId { get; set; }
            public string? CategoryName { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? UpdatedAt { get; set; }
            public string PriceRange { get; set; } = string.Empty;
            public int TotalStock { get; set; }
            public long TotalSold { get; set; }
            public int MinPreparationTime { get; set; }
            public int MinCalories { get; set; }
            public List<ProductTypeResponse> ProductTypes { get; set; } = new();
        }

        public class ProductTypeResponse
        {
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? ProductTypeImageUrl { get; set; }
            public double Price { get; set; }
            public int Stock { get; set; }
            public int Sold { get; set; }
            public DiscountType DiscountType { get; set; }
            public long? Discount { get; set; }
            public bool IsPublish { get; set; }
            public bool IsSpicy { get; set; }
            public bool IsVegetarian { get; set; }
            public int PreparationTime { get; set; }
            public int Calories { get; set; }
            public string Ingredients { get; set; } = string.Empty;
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
