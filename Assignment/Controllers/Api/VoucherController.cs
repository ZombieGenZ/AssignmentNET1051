using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.IO;
using Assignment.Data;
using Assignment.Enums;
using Assignment.Extensions;
using Assignment.Models;
using Assignment.Options;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Assignment.Controllers.Api
{
    [ApiController]
    [Route("api/vouchers")]
    [Authorize]
    public class VoucherController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthorizationService _authorizationService;
        private readonly UserManager<ApplicationUser> _userManager;

        public VoucherController(
            ApplicationDbContext context,
            IAuthorizationService authorizationService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _authorizationService = authorizationService;
            _userManager = userManager;
        }

        private string? CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        [HttpGet]
        public async Task<IActionResult> GetVouchers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = PaginationDefaults.DefaultPageSize,
            [FromQuery] string? search = null,
            [FromQuery] VoucherType? type = null,
            [FromQuery] bool? isPublish = null)
        {
            var canGetAll = User.HasPermission("GetVoucherAll");
            var canGetOwn = User.HasPermission("GetVoucher");

            if (!canGetAll && !canGetOwn)
            {
                return Forbid();
            }

            page = PaginationDefaults.NormalizePage(page);
            pageSize = PaginationDefaults.NormalizePageSize(pageSize);

            IQueryable<Voucher> query = _context.Vouchers
                .AsNoTracking()
                .Where(v => !v.IsDeleted);

            if (!canGetAll)
            {
                query = query.Where(v => v.CreateBy == CurrentUserId);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                var likeTerm = $"%{term}%";
                query = query.Where(v =>
                    EF.Functions.Like(v.Code, likeTerm) ||
                    EF.Functions.Like(v.Name, likeTerm) ||
                    EF.Functions.Like(v.Description ?? string.Empty, likeTerm));
            }

            if (type.HasValue)
            {
                query = query.Where(v => v.Type == type.Value);
            }

            if (isPublish.HasValue)
            {
                query = query.Where(v => v.IsPublish == isPublish.Value);
            }

            var totalItems = await query.CountAsync();
            var totalPages = totalItems == 0
                ? 0
                : (int)Math.Ceiling(totalItems / (double)pageSize);

            if (totalPages > 0 && page > totalPages)
            {
                page = totalPages;
            }

            var vouchers = await query
                .OrderByDescending(v => v.CreatedAt)
                .ThenBy(v => v.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var response = new PagedResponse<VoucherListItem>
            {
                CurrentPage = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = totalPages,
                PageSizeOptions = PaginationDefaults.PageSizeOptions.ToArray(),
                Items = vouchers.Select(MapToListItem).ToList()
            };

            return Ok(response);
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetVoucher(long id)
        {
            var voucher = await _context.Vouchers
                .Include(v => v.VoucherUsers)!
                .ThenInclude(vu => vu.User)
                .Include(v => v.VoucherProducts)
                .Include(v => v.VoucherCombos)
                .FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);

            if (voucher == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, voucher, "GetVoucherPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            var response = await BuildDetailResponse(voucher);
            return Ok(response);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CreateVoucherPolicy")]
        public async Task<IActionResult> CreateVoucher([FromBody] VoucherRequest request)
        {
            await ValidateVoucherRequest(request);
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var now = DateTime.Now;
            var voucher = new Voucher
            {
                Code = request.Code!.Trim(),
                Name = request.Name!.Trim(),
                Description = request.Description!.Trim(),
                Type = request.Type,
                ProductScope = request.ProductScope,
                ComboScope = request.ComboScope,
                Discount = request.Discount,
                DiscountType = request.DiscountType,
                Quantity = request.Quantity,
                StartTime = request.StartTime,
                IsLifeTime = request.IsLifeTime,
                EndTime = request.IsLifeTime ? null : request.EndTime,
                MinimumRequirements = request.MinimumRequirements,
                UnlimitedPercentageDiscount = request.UnlimitedPercentageDiscount,
                MaximumPercentageReduction = request.UnlimitedPercentageDiscount ? null : request.MaximumPercentageReduction,
                HasCombinedUsageLimit = request.HasCombinedUsageLimit,
                MaxCombinedUsageCount = request.HasCombinedUsageLimit ? request.MaxCombinedUsageCount : null,
                IsPublish = request.IsPublish,
                IsShow = request.Type == VoucherType.Public ? request.IsShow : false,
                Used = 0,
                CreateBy = CurrentUserId,
                CreatedAt = now,
                UpdatedAt = null,
                DeletedAt = null,
                IsDeleted = false
            };

            if (voucher.Type == VoucherType.Public)
            {
                voucher.UserId = null;
            }

            _context.Vouchers.Add(voucher);
            await _context.SaveChangesAsync();

            await PersistVoucherRelations(voucher, request);

            var createdVoucher = await _context.Vouchers
                .Include(v => v.VoucherUsers)!
                .ThenInclude(vu => vu.User)
                .Include(v => v.VoucherProducts)
                .Include(v => v.VoucherCombos)
                .FirstOrDefaultAsync(v => v.Id == voucher.Id);

            var response = await BuildDetailResponse(createdVoucher!);
            return CreatedAtAction(nameof(GetVoucher), new { id = voucher.Id }, response);
        }

        [HttpPut("{id:long}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateVoucher(long id, [FromBody] VoucherRequest request)
        {
            var voucher = await _context.Vouchers
                .Include(v => v.VoucherUsers)
                .Include(v => v.VoucherProducts)
                .Include(v => v.VoucherCombos)
                .FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);

            if (voucher == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, voucher, "UpdateVoucherPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            await ValidateVoucherRequest(request, voucher.Id);
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            voucher.Code = request.Code!.Trim();
            voucher.Name = request.Name!.Trim();
            voucher.Description = request.Description!.Trim();
            voucher.Type = request.Type;
            voucher.ProductScope = request.ProductScope;
            voucher.ComboScope = request.ComboScope;
            voucher.Discount = request.Discount;
            voucher.DiscountType = request.DiscountType;
            voucher.Quantity = request.Quantity;
            voucher.StartTime = request.StartTime;
            voucher.IsLifeTime = request.IsLifeTime;
            voucher.EndTime = request.IsLifeTime ? null : request.EndTime;
            voucher.MinimumRequirements = request.MinimumRequirements;
            voucher.UnlimitedPercentageDiscount = request.UnlimitedPercentageDiscount;
            voucher.MaximumPercentageReduction = request.UnlimitedPercentageDiscount ? null : request.MaximumPercentageReduction;
            voucher.HasCombinedUsageLimit = request.HasCombinedUsageLimit;
            voucher.MaxCombinedUsageCount = request.HasCombinedUsageLimit ? request.MaxCombinedUsageCount : null;
            voucher.IsPublish = request.IsPublish;
            voucher.IsShow = voucher.Type == VoucherType.Public ? request.IsShow : false;
            voucher.UserId = voucher.Type == VoucherType.Public ? null : voucher.UserId;
            voucher.UpdatedAt = DateTime.Now;

            await PersistVoucherRelations(voucher, request, update: true);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await VoucherExists(id))
                {
                    return NotFound();
                }

                throw;
            }

            var updatedVoucher = await _context.Vouchers
                .Include(v => v.VoucherUsers)!
                .ThenInclude(vu => vu.User)
                .Include(v => v.VoucherProducts)
                .Include(v => v.VoucherCombos)
                .FirstOrDefaultAsync(v => v.Id == voucher.Id);

            var response = await BuildDetailResponse(updatedVoucher!);
            return Ok(response);
        }

        [HttpDelete("{id:long}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteVoucher(long id)
        {
            var voucher = await _context.Vouchers
                .Include(v => v.VoucherUsers)
                .Include(v => v.VoucherProducts)
                .Include(v => v.VoucherCombos)
                .FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);

            if (voucher == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, voucher, "DeleteVoucherPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            var now = DateTime.Now;
            voucher.IsDeleted = true;
            voucher.DeletedAt = now;
            voucher.UpdatedAt = now;

            if (voucher.VoucherUsers != null)
            {
                foreach (var voucherUser in voucher.VoucherUsers)
                {
                    voucherUser.IsDeleted = true;
                    voucherUser.DeletedAt = now;
                }
            }

            if (voucher.VoucherProducts != null)
            {
                foreach (var voucherProduct in voucher.VoucherProducts)
                {
                    voucherProduct.IsDeleted = true;
                    voucherProduct.DeletedAt = now;
                }
            }

            if (voucher.VoucherCombos != null)
            {
                foreach (var voucherCombo in voucher.VoucherCombos)
                {
                    voucherCombo.IsDeleted = true;
                    voucherCombo.DeletedAt = now;
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
                return BadRequest(new { message = "Vui lòng chọn ít nhất một voucher để xóa." });
            }

            var vouchers = await _context.Vouchers
                .Where(v => request.Ids.Contains(v.Id) && !v.IsDeleted)
                .Include(v => v.VoucherUsers)
                .Include(v => v.VoucherProducts)
                .Include(v => v.VoucherCombos)
                .ToListAsync();

            if (!vouchers.Any())
            {
                return NotFound(new { message = "Không tìm thấy voucher hợp lệ để xóa." });
            }

            var now = DateTime.Now;
            var deletedCount = 0;
            var unauthorizedCount = 0;

            foreach (var voucher in vouchers)
            {
                var authResult = await _authorizationService.AuthorizeAsync(User, voucher, "DeleteVoucherPolicy");
                if (!authResult.Succeeded)
                {
                    unauthorizedCount++;
                    continue;
                }

                voucher.IsDeleted = true;
                voucher.DeletedAt = now;
                voucher.UpdatedAt = now;

                if (voucher.VoucherUsers != null)
                {
                    foreach (var voucherUser in voucher.VoucherUsers)
                    {
                        voucherUser.IsDeleted = true;
                        voucherUser.DeletedAt = now;
                    }
                }

                if (voucher.VoucherProducts != null)
                {
                    foreach (var voucherProduct in voucher.VoucherProducts)
                    {
                        voucherProduct.IsDeleted = true;
                        voucherProduct.DeletedAt = now;
                    }
                }

                if (voucher.VoucherCombos != null)
                {
                    foreach (var voucherCombo in voucher.VoucherCombos)
                    {
                        voucherCombo.IsDeleted = true;
                        voucherCombo.DeletedAt = now;
                    }
                }

                deletedCount++;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                deletedCount,
                unauthorizedCount,
                message = unauthorizedCount > 0
                    ? "Đã xóa các voucher được phép. Một số voucher không thể xóa do không đủ quyền."
                    : "Đã xóa voucher thành công."
            });
        }

        [HttpGet("form-options")]
        public async Task<IActionResult> GetFormOptions()
        {
            var canManage = User.HasAnyPermission(
                "CreateVoucher",
                "CreateVoucherAll",
                "UpdateVoucher",
                "UpdateVoucherAll");

            if (!canManage)
            {
                return Forbid();
            }

            var users = await _userManager.Users
                .OrderBy(u => u.Email ?? u.UserName ?? u.Id)
                .ToListAsync();

            var products = await _context.Products
                .Where(p => !p.IsDeleted)
                .OrderBy(p => p.Name)
                .Select(p => new ProductOption
                {
                    Id = p.Id,
                    Name = p.Name
                })
                .ToListAsync();

            var combos = await _context.Combos
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.Name)
                .Select(c => new ComboOption
                {
                    Id = c.Id,
                    Name = c.Name
                })
                .ToListAsync();

            var response = new FormOptionsResponse
            {
                Users = users.Select(user => new UserOption
                {
                    Id = user.Id,
                    DisplayName = BuildUserDisplayName(user)
                }).ToList(),
                Products = products,
                Combos = combos
            };

            return Ok(response);
        }

        [HttpPost("import-users")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportUsers([FromForm] IFormFile? file)
        {
            if (!User.HasAnyPermission(
                    "CreateVoucher",
                    "CreateVoucherAll",
                    "UpdateVoucher",
                    "UpdateVoucherAll"))
            {
                return Forbid();
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "Vui lòng chọn file chứa danh sách người dùng." });
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
                    return BadRequest(new { message = "File không chứa dữ liệu người dùng." });
                }

                var userIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var row in worksheet.RowsUsed())
                {
                    var rawValue = row.Cell(1).GetString()?.Trim();
                    if (string.IsNullOrWhiteSpace(rawValue))
                    {
                        continue;
                    }

                    if (row.RowNumber() == 1 && string.Equals(rawValue, "UserId", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    userIds.Add(rawValue);
                }

                if (!userIds.Any())
                {
                    return Ok(new ImportUsersResponse());
                }

                var normalizedIds = userIds.ToList();
                var users = await _userManager.Users
                    .Where(u => normalizedIds.Contains(u.Id))
                    .ToListAsync();

                var foundIds = new HashSet<string>(users.Select(u => u.Id), StringComparer.OrdinalIgnoreCase);
                var invalidIds = normalizedIds
                    .Where(id => !foundIds.Contains(id))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var response = new ImportUsersResponse
                {
                    Users = users.Select(user => new UserOption
                    {
                        Id = user.Id,
                        DisplayName = BuildUserDisplayName(user)
                    }).ToList(),
                    InvalidEntries = invalidIds
                };

                return Ok(response);
            }
            catch (Exception)
            {
                return BadRequest(new { message = "File không hợp lệ hoặc bị hỏng." });
            }
        }

        [HttpPost("import-products")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportProducts([FromForm] IFormFile? file)
        {
            if (!User.HasAnyPermission(
                    "CreateVoucher",
                    "CreateVoucherAll",
                    "UpdateVoucher",
                    "UpdateVoucherAll"))
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

                var productIds = new HashSet<long>();
                var invalidEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

                    productIds.Add(productId);
                }

                if (!productIds.Any())
                {
                    return Ok(new ImportProductsResponse
                    {
                        InvalidEntries = invalidEntries.ToList()
                    });
                }

                var products = await _context.Products
                    .Where(p => productIds.Contains(p.Id) && !p.IsDeleted)
                    .Select(p => new ProductOption
                    {
                        Id = p.Id,
                        Name = p.Name
                    })
                    .ToListAsync();

                var foundIds = new HashSet<long>(products.Select(p => p.Id));
                foreach (var id in productIds)
                {
                    if (!foundIds.Contains(id))
                    {
                        invalidEntries.Add(id.ToString());
                    }
                }

                var response = new ImportProductsResponse
                {
                    Products = products,
                    InvalidEntries = invalidEntries.ToList()
                };

                return Ok(response);
            }
            catch (Exception)
            {
                return BadRequest(new { message = "File không hợp lệ hoặc bị hỏng." });
            }
        }

        [HttpPost("import-combos")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportCombos([FromForm] IFormFile? file)
        {
            if (!User.HasAnyPermission(
                    "CreateVoucher",
                    "CreateVoucherAll",
                    "UpdateVoucher",
                    "UpdateVoucherAll"))
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

                var comboIds = new HashSet<long>();
                var invalidEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

                    comboIds.Add(comboId);
                }

                if (!comboIds.Any())
                {
                    return Ok(new ImportCombosResponse
                    {
                        InvalidEntries = invalidEntries.ToList()
                    });
                }

                var combos = await _context.Combos
                    .Where(c => comboIds.Contains(c.Id) && !c.IsDeleted)
                    .Select(c => new ComboOption
                    {
                        Id = c.Id,
                        Name = c.Name
                    })
                    .ToListAsync();

                var foundIds = new HashSet<long>(combos.Select(c => c.Id));
                foreach (var id in comboIds)
                {
                    if (!foundIds.Contains(id))
                    {
                        invalidEntries.Add(id.ToString());
                    }
                }

                var response = new ImportCombosResponse
                {
                    Combos = combos,
                    InvalidEntries = invalidEntries.ToList()
                };

                return Ok(response);
            }
            catch (Exception)
            {
                return BadRequest(new { message = "File không hợp lệ hoặc bị hỏng." });
            }
        }

        [HttpGet("product-template")]
        public IActionResult DownloadVoucherProductTemplate()
        {
            if (!User.HasAnyPermission("CreateVoucher", "CreateVoucherAll", "UpdateVoucher", "UpdateVoucherAll"))
            {
                return Forbid();
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("VoucherProducts");
            worksheet.Cell(1, 1).Value = "ProductId";
            worksheet.Cell(2, 1).Value = "123";

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            var content = stream.ToArray();
            const string fileName = "voucher_products_template.xlsx";
            const string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            return File(content, contentType, fileName);
        }

        [HttpGet("combo-template")]
        public IActionResult DownloadVoucherComboTemplate()
        {
            if (!User.HasAnyPermission("CreateVoucher", "CreateVoucherAll", "UpdateVoucher", "UpdateVoucherAll"))
            {
                return Forbid();
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("VoucherCombos");
            worksheet.Cell(1, 1).Value = "ComboId";
            worksheet.Cell(2, 1).Value = "456";

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            var content = stream.ToArray();
            const string fileName = "voucher_combos_template.xlsx";
            const string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            return File(content, contentType, fileName);
        }

        [HttpGet("user-template")]
        public IActionResult DownloadVoucherTemplate()
        {
            if (!User.HasAnyPermission("CreateVoucher", "CreateVoucherAll", "UpdateVoucher", "UpdateVoucherAll"))
            {
                return Forbid();
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("VoucherUsers");
            worksheet.Cell(1, 1).Value = "UserId";
            worksheet.Cell(2, 1).Value = "sample-user-id";

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            var content = stream.ToArray();
            const string fileName = "voucher_users_template.xlsx";
            const string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            return File(content, contentType, fileName);
        }

        private async Task ValidateVoucherRequest(VoucherRequest request, long? voucherId = null)
        {
            if (string.IsNullOrWhiteSpace(request.Code))
            {
                ModelState.AddModelError(nameof(request.Code), "Vui lòng nhập mã voucher.");
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                ModelState.AddModelError(nameof(request.Name), "Vui lòng nhập tên voucher.");
            }

            if (string.IsNullOrWhiteSpace(request.Description))
            {
                ModelState.AddModelError(nameof(request.Description), "Vui lòng nhập mô tả voucher.");
            }

            if (request.HasCombinedUsageLimit && (!request.MaxCombinedUsageCount.HasValue || request.MaxCombinedUsageCount.Value < 1))
            {
                ModelState.AddModelError(nameof(request.MaxCombinedUsageCount), "Vui lòng nhập số voucher áp dụng chung tối đa hợp lệ.");
            }

            if (!request.HasCombinedUsageLimit)
            {
                request.MaxCombinedUsageCount = null;
            }

            if (request.DiscountType == VoucherDiscountType.Percent && request.UnlimitedPercentageDiscount == false)
            {
                if (!request.MaximumPercentageReduction.HasValue || request.MaximumPercentageReduction.Value <= 0)
                {
                    ModelState.AddModelError(nameof(request.MaximumPercentageReduction), "Vui lòng nhập mức giảm tối đa hợp lệ.");
                }
            }
            else if (request.UnlimitedPercentageDiscount)
            {
                request.MaximumPercentageReduction = null;
            }

            if (request.ProductScope == VoucherProductScope.SelectedProducts)
            {
                request.ProductIds = request.ProductIds?
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList() ?? new List<long>();

                if (request.ProductIds.Count == 0)
                {
                    ModelState.AddModelError(nameof(request.ProductIds), "Vui lòng chọn ít nhất một sản phẩm áp dụng.");
                }
                else
                {
                    var validProductIds = await _context.Products
                        .Where(p => request.ProductIds.Contains(p.Id) && !p.IsDeleted)
                        .Select(p => p.Id)
                        .ToListAsync();

                    if (validProductIds.Count != request.ProductIds.Count)
                    {
                        ModelState.AddModelError(nameof(request.ProductIds), "Có sản phẩm không hợp lệ trong danh sách đã chọn.");
                    }

                    request.ProductIds = validProductIds;
                }
            }
            else
            {
                request.ProductIds = new List<long>();
            }

            if (request.ComboScope == VoucherComboScope.SelectedCombos)
            {
                request.ComboIds = request.ComboIds?
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList() ?? new List<long>();

                if (request.ComboIds.Count == 0)
                {
                    ModelState.AddModelError(nameof(request.ComboIds), "Vui lòng chọn ít nhất một combo áp dụng.");
                }
                else
                {
                    var validComboIds = await _context.Combos
                        .Where(c => request.ComboIds.Contains(c.Id) && !c.IsDeleted)
                        .Select(c => c.Id)
                        .ToListAsync();

                    if (validComboIds.Count != request.ComboIds.Count)
                    {
                        ModelState.AddModelError(nameof(request.ComboIds), "Có combo không hợp lệ trong danh sách đã chọn.");
                    }

                    request.ComboIds = validComboIds;
                }
            }
            else
            {
                request.ComboIds = new List<long>();
            }

            if (request.ProductScope == VoucherProductScope.NoProducts && request.ComboScope == VoucherComboScope.NoCombos)
            {
                ModelState.AddModelError(nameof(request.ComboScope), "Voucher phải áp dụng cho ít nhất sản phẩm hoặc combo.");
            }

            if (request.Type == VoucherType.Private)
            {
                request.UserIds = request.UserIds?
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct()
                    .ToList() ?? new List<string>();

                if (request.UserIds.Count == 0)
                {
                    ModelState.AddModelError(nameof(request.UserIds), "Voucher riêng tư cần ít nhất một người dùng.");
                }
                else
                {
                    var validUserIds = await _userManager.Users
                        .Where(u => request.UserIds.Contains(u.Id))
                        .Select(u => u.Id)
                        .ToListAsync();

                    if (validUserIds.Count != request.UserIds.Count)
                    {
                        ModelState.AddModelError(nameof(request.UserIds), "Có người dùng không hợp lệ trong danh sách đã chọn.");
                    }

                    request.UserIds = validUserIds;
                }
            }
            else
            {
                request.UserIds = new List<string>();
                request.IsShow = request.Type == VoucherType.Public && request.IsShow;
            }

            if (!request.IsLifeTime)
            {
                if (!request.EndTime.HasValue)
                {
                    ModelState.AddModelError(nameof(request.EndTime), "Vui lòng chọn thời gian kết thúc.");
                }
                else if (request.EndTime <= request.StartTime)
                {
                    ModelState.AddModelError(nameof(request.EndTime), "Thời gian kết thúc phải sau thời gian bắt đầu.");
                }
            }
            else
            {
                request.EndTime = null;
            }

            var normalizedCode = request.Code?.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedCode))
            {
                var codeExists = await _context.Vouchers.AnyAsync(v => v.Code == normalizedCode && !v.IsDeleted && v.Id != voucherId);
                if (codeExists)
                {
                    ModelState.AddModelError(nameof(request.Code), "Mã voucher này đã tồn tại. Vui lòng chọn mã khác.");
                }
            }
        }

        private async Task PersistVoucherRelations(Voucher voucher, VoucherRequest request, bool update = false)
        {
            if (update)
            {
                var existingUsers = await _context.VoucherUsers
                    .Where(vu => vu.VoucherId == voucher.Id && !vu.IsSaved)
                    .ToListAsync();

                if (existingUsers.Any())
                {
                    _context.VoucherUsers.RemoveRange(existingUsers);
                }

                var existingProducts = await _context.VoucherProducts
                    .Where(vp => vp.VoucherId == voucher.Id)
                    .ToListAsync();

                if (existingProducts.Any())
                {
                    _context.VoucherProducts.RemoveRange(existingProducts);
                }

                var existingCombos = await _context.VoucherCombos
                    .Where(vc => vc.VoucherId == voucher.Id)
                    .ToListAsync();

                if (existingCombos.Any())
                {
                    _context.VoucherCombos.RemoveRange(existingCombos);
                }

                await _context.SaveChangesAsync();
            }

            var currentUserId = CurrentUserId;
            var now = DateTime.Now;

            if (request.UserIds != null && request.UserIds.Any())
            {
                foreach (var userId in request.UserIds)
                {
                    var voucherUser = new VoucherUser
                    {
                        VoucherId = voucher.Id,
                        UserId = userId,
                        CreateBy = currentUserId,
                        CreatedAt = now,
                        UpdatedAt = null,
                        DeletedAt = null,
                        IsDeleted = false,
                        IsSaved = false
                    };

                    _context.VoucherUsers.Add(voucherUser);
                }
            }

            if (request.ProductIds != null && request.ProductIds.Any())
            {
                foreach (var productId in request.ProductIds)
                {
                    var voucherProduct = new VoucherProduct
                    {
                        VoucherId = voucher.Id,
                        ProductId = productId,
                        CreateBy = currentUserId,
                        CreatedAt = now,
                        UpdatedAt = null,
                        DeletedAt = null,
                        IsDeleted = false
                    };

                    _context.VoucherProducts.Add(voucherProduct);
                }
            }

            if (request.ComboIds != null && request.ComboIds.Any())
            {
                foreach (var comboId in request.ComboIds)
                {
                    var voucherCombo = new VoucherCombo
                    {
                        VoucherId = voucher.Id,
                        ComboId = comboId,
                        CreateBy = currentUserId,
                        CreatedAt = now,
                        UpdatedAt = null,
                        DeletedAt = null,
                        IsDeleted = false
                    };

                    _context.VoucherCombos.Add(voucherCombo);
                }
            }

            await _context.SaveChangesAsync();
        }

        private async Task<bool> VoucherExists(long id)
        {
            return await _context.Vouchers.AnyAsync(v => v.Id == id && !v.IsDeleted);
        }

        private static VoucherListItem MapToListItem(Voucher voucher)
        {
            return new VoucherListItem
            {
                Id = voucher.Id,
                Code = voucher.Code,
                Name = voucher.Name,
                Type = voucher.Type,
                ProductScope = voucher.ProductScope,
                ComboScope = voucher.ComboScope,
                DiscountType = voucher.DiscountType,
                Discount = voucher.Discount,
                Used = voucher.Used,
                Quantity = voucher.Quantity,
                HasCombinedUsageLimit = voucher.HasCombinedUsageLimit,
                MaxCombinedUsageCount = voucher.MaxCombinedUsageCount,
                IsPublish = voucher.IsPublish,
                IsShow = voucher.IsShow,
                IsLifeTime = voucher.IsLifeTime,
                StartTime = voucher.StartTime,
                EndTime = voucher.EndTime,
                MinimumRequirements = voucher.MinimumRequirements,
                UnlimitedPercentageDiscount = voucher.UnlimitedPercentageDiscount,
                MaximumPercentageReduction = voucher.MaximumPercentageReduction,
                CreatedAt = voucher.CreatedAt,
                UpdatedAt = voucher.UpdatedAt
            };
        }

        private async Task<VoucherDetailResponse> BuildDetailResponse(Voucher voucher)
        {
            var selectedUserIds = voucher.VoucherUsers?
                .Where(vu => !vu.IsDeleted && !vu.IsSaved)
                .Select(vu => vu.UserId)
                .Where(id => id != null)
                .Cast<string>()
                .Distinct()
                .ToList() ?? new List<string>();

            var userLookup = new Dictionary<string, string>();
            if (selectedUserIds.Any())
            {
                var users = await _userManager.Users
                    .Where(u => selectedUserIds.Contains(u.Id))
                    .ToListAsync();

                foreach (var user in users)
                {
                    userLookup[user.Id] = BuildUserDisplayName(user);
                }
            }

            var selectedProductIds = voucher.VoucherProducts?
                .Where(vp => !vp.IsDeleted)
                .Select(vp => vp.ProductId)
                .Distinct()
                .ToList() ?? new List<long>();

            var productLookup = new Dictionary<long, string>();
            if (selectedProductIds.Any())
            {
                var products = await _context.Products
                    .Where(p => selectedProductIds.Contains(p.Id))
                    .Select(p => new { p.Id, p.Name })
                    .ToListAsync();

                foreach (var product in products)
                {
                    productLookup[product.Id] = product.Name;
                }
            }

            var selectedComboIds = voucher.VoucherCombos?
                .Where(vc => !vc.IsDeleted)
                .Select(vc => vc.ComboId)
                .Distinct()
                .ToList() ?? new List<long>();

            var comboLookup = new Dictionary<long, string>();
            if (selectedComboIds.Any())
            {
                var combos = await _context.Combos
                    .Where(c => selectedComboIds.Contains(c.Id))
                    .Select(c => new { c.Id, c.Name })
                    .ToListAsync();

                foreach (var combo in combos)
                {
                    comboLookup[combo.Id] = combo.Name;
                }
            }

            return new VoucherDetailResponse
            {
                Id = voucher.Id,
                Code = voucher.Code,
                Name = voucher.Name,
                Description = voucher.Description,
                Type = voucher.Type,
                ProductScope = voucher.ProductScope,
                ComboScope = voucher.ComboScope,
                Discount = voucher.Discount,
                DiscountType = voucher.DiscountType,
                Used = voucher.Used,
                Quantity = voucher.Quantity,
                StartTime = voucher.StartTime,
                IsLifeTime = voucher.IsLifeTime,
                EndTime = voucher.EndTime,
                MinimumRequirements = voucher.MinimumRequirements,
                UnlimitedPercentageDiscount = voucher.UnlimitedPercentageDiscount,
                MaximumPercentageReduction = voucher.MaximumPercentageReduction,
                HasCombinedUsageLimit = voucher.HasCombinedUsageLimit,
                MaxCombinedUsageCount = voucher.MaxCombinedUsageCount,
                IsPublish = voucher.IsPublish,
                IsShow = voucher.IsShow,
                CreatedAt = voucher.CreatedAt,
                UpdatedAt = voucher.UpdatedAt,
                Users = selectedUserIds.Select(id => new UserOption
                {
                    Id = id,
                    DisplayName = userLookup.TryGetValue(id, out var display) ? display : id
                }).ToList(),
                Products = selectedProductIds.Select(id => new ProductOption
                {
                    Id = id,
                    Name = productLookup.TryGetValue(id, out var name) ? name : $"Sản phẩm #{id}"
                }).ToList(),
                Combos = selectedComboIds.Select(id => new ComboOption
                {
                    Id = id,
                    Name = comboLookup.TryGetValue(id, out var name) ? name : $"Combo #{id}"
                }).ToList()
            };
        }

        private static string BuildUserDisplayName(ApplicationUser user)
        {
            if (user == null)
            {
                return string.Empty;
            }

            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(user.FullName))
            {
                parts.Add(user.FullName);
            }

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                parts.Add(user.Email);
            }

            if (!string.IsNullOrWhiteSpace(user.UserName) &&
                !string.Equals(user.UserName, user.Id, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(user.UserName, user.Email, StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(user.UserName);
            }

            if (!string.IsNullOrWhiteSpace(user.Id))
            {
                parts.Add($"ID: {user.Id}");
            }

            var distinctParts = new List<string>();
            foreach (var part in parts)
            {
                if (!distinctParts.Any(existing => string.Equals(existing, part, StringComparison.OrdinalIgnoreCase)))
                {
                    distinctParts.Add(part);
                }
            }

            if (!distinctParts.Any())
            {
                return string.Empty;
            }

            if (distinctParts.Count == 1 && distinctParts[0].StartsWith("ID:", StringComparison.OrdinalIgnoreCase))
            {
                return $"Người dùng chưa đặt tên ({distinctParts[0]})";
            }

            var primary = distinctParts[0];
            var extras = distinctParts.Skip(1).ToList();

            return extras.Any()
                ? $"{primary} ({string.Join(", ", extras)})"
                : primary;
        }

        public class VoucherRequest
        {
            [Required]
            [StringLength(100)]
            public string? Code { get; set; }

            [Required]
            [StringLength(300)]
            public string? Name { get; set; }

            [Required]
            [StringLength(1000)]
            public string? Description { get; set; }

            public VoucherType Type { get; set; }

            public VoucherProductScope ProductScope { get; set; }

            public VoucherComboScope ComboScope { get; set; }

            [Range(0, double.MaxValue)]
            public double Discount { get; set; }

            public VoucherDiscountType DiscountType { get; set; }

            [Range(0, long.MaxValue)]
            public long Quantity { get; set; }

            public DateTime StartTime { get; set; }

            public bool IsLifeTime { get; set; }

            public DateTime? EndTime { get; set; }

            [Range(0, double.MaxValue)]
            public double MinimumRequirements { get; set; }

            public bool UnlimitedPercentageDiscount { get; set; }

            [Range(0, double.MaxValue)]
            public double? MaximumPercentageReduction { get; set; }

            public bool HasCombinedUsageLimit { get; set; }

            [Range(1, int.MaxValue)]
            public int? MaxCombinedUsageCount { get; set; }

            public bool IsPublish { get; set; }

            public bool IsShow { get; set; }

            public List<string>? UserIds { get; set; }

            public List<long>? ProductIds { get; set; }

            public List<long>? ComboIds { get; set; }
        }

        public class VoucherListItem
        {
            public long Id { get; set; }
            public string Code { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public VoucherType Type { get; set; }
            public VoucherProductScope ProductScope { get; set; }
            public VoucherComboScope ComboScope { get; set; }
            public VoucherDiscountType DiscountType { get; set; }
            public double Discount { get; set; }
            public long Used { get; set; }
            public long Quantity { get; set; }
            public bool HasCombinedUsageLimit { get; set; }
            public int? MaxCombinedUsageCount { get; set; }
            public bool IsPublish { get; set; }
            public bool IsShow { get; set; }
            public bool IsLifeTime { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime? EndTime { get; set; }
            public double MinimumRequirements { get; set; }
            public bool UnlimitedPercentageDiscount { get; set; }
            public double? MaximumPercentageReduction { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? UpdatedAt { get; set; }
        }

        public class VoucherDetailResponse : VoucherListItem
        {
            public string Description { get; set; } = string.Empty;
            public List<UserOption> Users { get; set; } = new();
            public List<ProductOption> Products { get; set; } = new();
            public List<ComboOption> Combos { get; set; } = new();
        }

        public class UserOption
        {
            public string Id { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
        }

        public class ProductOption
        {
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public class ComboOption
        {
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public class FormOptionsResponse
        {
            public List<UserOption> Users { get; set; } = new();
            public List<ProductOption> Products { get; set; } = new();
            public List<ComboOption> Combos { get; set; } = new();
        }

        public class ImportUsersResponse
        {
            public List<UserOption> Users { get; set; } = new();
            public List<string> InvalidEntries { get; set; } = new();
        }

        public class ImportProductsResponse
        {
            public List<ProductOption> Products { get; set; } = new();
            public List<string> InvalidEntries { get; set; } = new();
        }

        public class ImportCombosResponse
        {
            public List<ComboOption> Combos { get; set; } = new();
            public List<string> InvalidEntries { get; set; } = new();
        }

        public class PagedResponse<T>
        {
            public int CurrentPage { get; set; }
            public int PageSize { get; set; }
            public int TotalItems { get; set; }
            public int TotalPages { get; set; }
            public IReadOnlyList<int> PageSizeOptions { get; set; } = Array.Empty<int>();
            public IReadOnlyCollection<T> Items { get; set; } = Array.Empty<T>();
        }

        public class BulkDeleteRequest
        {
            public List<long> Ids { get; set; } = new();
        }
    }
}
