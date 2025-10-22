using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using System.IO;
using Assignment.Data;
using Assignment.Enums;
using Assignment.Models;
using Assignment.Options;
using Assignment.Extensions;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Collections.Generic;

namespace Assignment.Controllers.Api
{
    [ApiController]
    [Route("api/rewards")]
    [Authorize]
    public class RewardController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthorizationService _authorizationService;

        public RewardController(ApplicationDbContext context, IAuthorizationService authorizationService)
        {
            _context = context;
            _authorizationService = authorizationService;
        }

        private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

        private IQueryable<Reward> BuildRewardQuery(bool tracking = false)
        {
            var query = tracking ? _context.Rewards.AsQueryable() : _context.Rewards.AsNoTracking();

            return query
                .Include(r => r.RewardProducts.Where(rp => !rp.IsDeleted))
                    .ThenInclude(rp => rp.Product)
                .Include(r => r.RewardCombos.Where(rc => !rc.IsDeleted))
                    .ThenInclude(rc => rc.Combo);
        }

        [HttpGet]
        public async Task<IActionResult> GetRewards(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = PaginationDefaults.DefaultPageSize,
            [FromQuery] string? search = null,
            [FromQuery] bool? isPublish = null)
        {
            var canGetAll = User.HasPermission("GetRewardAll");
            var canGetOwn = User.HasPermission("GetReward");

            if (!canGetAll && !canGetOwn)
            {
                return Forbid();
            }

            page = PaginationDefaults.NormalizePage(page);
            pageSize = PaginationDefaults.NormalizePageSize(pageSize);

            IQueryable<Reward> query = BuildRewardQuery()
                .Where(r => !r.IsDeleted);

            if (!canGetAll)
            {
                var currentUserId = CurrentUserId;
                query = query.Where(r => r.CreateBy == currentUserId);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                var like = $"%{term}%";
                query = query.Where(r =>
                    EF.Functions.Like(r.Name, like) ||
                    EF.Functions.Like(r.Description, like));
            }

            if (isPublish.HasValue)
            {
                query = query.Where(r => r.IsPublish == isPublish.Value);
            }

            var totalItems = await query.CountAsync();
            var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
            if (totalPages > 0 && page > totalPages)
            {
                page = totalPages;
            }

            var rewards = await query
                .OrderByDescending(r => r.CreatedAt)
                .ThenBy(r => r.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var response = new PagedResponse<RewardListItem>
            {
                CurrentPage = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = totalPages,
                PageSizeOptions = PaginationDefaults.PageSizeOptions.ToArray(),
                Items = rewards.Select(MapToListItem).ToList()
            };

            return Ok(response);
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetReward(long id)
        {
            var reward = await BuildRewardQuery()
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
            if (reward == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, reward, "GetRewardPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            return Ok(MapToDetail(reward));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CreateRewardPolicy")]
        public async Task<IActionResult> CreateReward([FromBody] RewardRequest request)
        {
            await ValidateRewardRequestAsync(request);
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var now = DateTime.Now;
            var reward = new Reward
            {
                Type = request.Type,
                Name = request.Name!.Trim(),
                Description = request.Description!.Trim(),
                PointCost = request.PointCost,
                MinimumRank = request.MinimumRank,
                Quantity = request.IsQuantityUnlimited ? 0 : request.Quantity,
                IsQuantityUnlimited = request.IsQuantityUnlimited,
                Redeemed = 0,
                ValidityValue = request.IsValidityUnlimited ? 0 : request.ValidityValue,
                ValidityUnit = request.ValidityUnit,
                IsValidityUnlimited = request.IsValidityUnlimited,
                IsPublish = request.IsPublish,
                VoucherProductScope = request.VoucherProductScope,
                VoucherComboScope = request.VoucherComboScope,
                VoucherDiscountType = request.VoucherDiscountType,
                VoucherDiscount = request.VoucherDiscount,
                VoucherMinimumRequirements = request.VoucherMinimumRequirements,
                VoucherUnlimitedPercentageDiscount = request.VoucherUnlimitedPercentageDiscount,
                VoucherMaximumPercentageReduction = request.VoucherMaximumPercentageReduction,
                VoucherHasCombinedUsageLimit = request.VoucherHasCombinedUsageLimit,
                VoucherMaxCombinedUsageCount = request.VoucherHasCombinedUsageLimit ? request.VoucherMaxCombinedUsageCount : null,
                VoucherIsForNewUsersOnly = request.VoucherIsForNewUsersOnly,
                VoucherQuantity = request.VoucherQuantity,
                CreateBy = CurrentUserId,
                CreatedAt = now,
                UpdatedAt = null,
                DeletedAt = null,
                IsDeleted = false
            };

            _context.Rewards.Add(reward);
            await _context.SaveChangesAsync();

            await UpdateRewardAssociationsAsync(reward, request, now);
            await _context.SaveChangesAsync();

            var createdReward = await BuildRewardQuery()
                .FirstOrDefaultAsync(r => r.Id == reward.Id);

            return CreatedAtAction(nameof(GetReward), new { id = reward.Id }, MapToDetail(createdReward ?? reward));
        }

        [HttpPut("{id:long}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateReward(long id, [FromBody] RewardRequest request)
        {
            var reward = await BuildRewardQuery(tracking: true)
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
            if (reward == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, reward, "UpdateRewardPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            await ValidateRewardRequestAsync(request);
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            reward.Type = request.Type;
            reward.Name = request.Name!.Trim();
            reward.Description = request.Description!.Trim();
            reward.PointCost = request.PointCost;
            reward.MinimumRank = request.MinimumRank;
            reward.Quantity = request.IsQuantityUnlimited ? 0 : request.Quantity;
            reward.IsQuantityUnlimited = request.IsQuantityUnlimited;
            reward.ValidityValue = request.IsValidityUnlimited ? 0 : request.ValidityValue;
            reward.ValidityUnit = request.ValidityUnit;
            reward.IsValidityUnlimited = request.IsValidityUnlimited;
            reward.IsPublish = request.IsPublish;
            reward.VoucherProductScope = request.VoucherProductScope;
            reward.VoucherComboScope = request.VoucherComboScope;
            reward.VoucherDiscountType = request.VoucherDiscountType;
            reward.VoucherDiscount = request.VoucherDiscount;
            reward.VoucherMinimumRequirements = request.VoucherMinimumRequirements;
            reward.VoucherUnlimitedPercentageDiscount = request.VoucherUnlimitedPercentageDiscount;
            reward.VoucherMaximumPercentageReduction = request.VoucherMaximumPercentageReduction;
            reward.VoucherHasCombinedUsageLimit = request.VoucherHasCombinedUsageLimit;
            reward.VoucherMaxCombinedUsageCount = request.VoucherHasCombinedUsageLimit ? request.VoucherMaxCombinedUsageCount : null;
            reward.VoucherIsForNewUsersOnly = request.VoucherIsForNewUsersOnly;
            reward.VoucherQuantity = request.VoucherQuantity;
            reward.UpdatedAt = DateTime.Now;

            await UpdateRewardAssociationsAsync(reward, request, reward.UpdatedAt.Value);
            await _context.SaveChangesAsync();

            var updatedReward = await BuildRewardQuery()
                .FirstOrDefaultAsync(r => r.Id == reward.Id);

            return Ok(MapToDetail(updatedReward ?? reward));
        }

        [HttpDelete("{id:long}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReward(long id)
        {
            var reward = await _context.Rewards.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
            if (reward == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, reward, "DeleteRewardPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            reward.IsDeleted = true;
            reward.DeletedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("bulk-delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteRequest request)
        {
            if (request == null || request.Ids == null || request.Ids.Count == 0)
            {
                return BadRequest(new { message = "Vui lòng chọn ít nhất một vật phẩm để xóa." });
            }

            var rewards = await _context.Rewards
                .Where(r => request.Ids.Contains(r.Id) && !r.IsDeleted)
                .ToListAsync();

            if (rewards.Count == 0)
            {
                return NotFound(new { message = "Không tìm thấy vật phẩm đổi thưởng để xóa." });
            }

            foreach (var reward in rewards)
            {
                var authResult = await _authorizationService.AuthorizeAsync(User, reward, "DeleteRewardPolicy");
                if (!authResult.Succeeded)
                {
                    return Forbid();
                }

                reward.IsDeleted = true;
                reward.DeletedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã xóa các vật phẩm đổi thưởng đã chọn." });
        }

        [HttpPost("bulk-publish")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkPublish([FromBody] BulkPublishRequest request)
        {
            if (request?.Ids == null || request.Ids.Count == 0)
            {
                return BadRequest(new { message = "Vui lòng chọn ít nhất một vật phẩm để cập nhật." });
            }

            var rewards = await _context.Rewards
                .Where(r => request.Ids.Contains(r.Id) && !r.IsDeleted)
                .ToListAsync();

            if (!rewards.Any())
            {
                return NotFound(new { message = "Không tìm thấy vật phẩm hợp lệ để cập nhật." });
            }

            var updatedCount = 0;
            var unauthorizedCount = 0;
            var now = DateTime.Now;

            foreach (var reward in rewards)
            {
                var authResult = await _authorizationService.AuthorizeAsync(User, reward, "UpdateRewardPolicy");
                if (!authResult.Succeeded)
                {
                    unauthorizedCount++;
                    continue;
                }

                if (reward.IsPublish == request.IsPublish)
                {
                    continue;
                }

                reward.IsPublish = request.IsPublish;
                reward.UpdatedAt = now;
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

        [HttpPost("import-products")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportProducts([FromForm] IFormFile? file)
        {
            if (!User.HasAnyPermission(
                    "CreateReward",
                    "CreateRewardAll",
                    "UpdateReward",
                    "UpdateRewardAll"))
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
                    "CreateReward",
                    "CreateRewardAll",
                    "UpdateReward",
                    "UpdateRewardAll"))
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
        public IActionResult DownloadRewardProductTemplate()
        {
            if (!User.HasAnyPermission("CreateReward", "CreateRewardAll", "UpdateReward", "UpdateRewardAll"))
            {
                return Forbid();
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("RewardProducts");
            worksheet.Cell(1, 1).Value = "ProductId";
            worksheet.Cell(2, 1).Value = "123";

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            var content = stream.ToArray();
            const string fileName = "reward_products_template.xlsx";
            const string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            return File(content, contentType, fileName);
        }

        [HttpGet("combo-template")]
        public IActionResult DownloadRewardComboTemplate()
        {
            if (!User.HasAnyPermission("CreateReward", "CreateRewardAll", "UpdateReward", "UpdateRewardAll"))
            {
                return Forbid();
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("RewardCombos");
            worksheet.Cell(1, 1).Value = "ComboId";
            worksheet.Cell(2, 1).Value = "456";

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            var content = stream.ToArray();
            const string fileName = "reward_combos_template.xlsx";
            const string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            return File(content, contentType, fileName);
        }

        [HttpGet("form-options")]
        public async Task<IActionResult> GetFormOptions()
        {
            if (!User.HasAnyPermission(
                    "CreateReward",
                    "CreateRewardAll",
                    "UpdateReward",
                    "UpdateRewardAll"))
            {
                return Forbid();
            }

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
                Products = products,
                Combos = combos
            };

            return Ok(response);
        }

        private async Task ValidateRewardRequestAsync(RewardRequest request)
        {
            ValidateRewardRequestCore(request);
            if (!ModelState.IsValid)
            {
                return;
            }

            await ValidateRewardScopeSelectionsAsync(request);
        }

        private void ValidateRewardRequestCore(RewardRequest request)
        {
            if (request.Type != RewardItemType.Voucher)
            {
                ModelState.AddModelError(nameof(request.Type), "Loại vật phẩm đổi thưởng không hợp lệ.");
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                ModelState.AddModelError(nameof(request.Name), "Vui lòng nhập tên vật phẩm.");
            }

            if (string.IsNullOrWhiteSpace(request.Description))
            {
                ModelState.AddModelError(nameof(request.Description), "Vui lòng nhập mô tả vật phẩm.");
            }

            if (request.PointCost < 0)
            {
                ModelState.AddModelError(nameof(request.PointCost), "Điểm đổi thưởng phải lớn hơn hoặc bằng 0.");
            }

            if (request.IsQuantityUnlimited)
            {
                request.Quantity = 0;
            }
            else if (request.Quantity <= 0)
            {
                ModelState.AddModelError(nameof(request.Quantity), "Vui lòng nhập số lượng lớn hơn 0 hoặc chọn không giới hạn.");
            }

            if (!request.IsValidityUnlimited && request.ValidityValue < 1)
            {
                ModelState.AddModelError(nameof(request.ValidityValue), "Thời hạn hiệu lực phải lớn hơn hoặc bằng 1.");
            }

            if (request.IsValidityUnlimited)
            {
                request.ValidityUnit = RewardValidityUnit.Forever;
                request.ValidityValue = 0;
            }

            if (request.VoucherDiscount <= 0)
            {
                ModelState.AddModelError(nameof(request.VoucherDiscount), "Giá trị giảm phải lớn hơn 0.");
            }

            if (request.VoucherMinimumRequirements < 0)
            {
                ModelState.AddModelError(nameof(request.VoucherMinimumRequirements), "Đơn tối thiểu phải lớn hơn hoặc bằng 0.");
            }

            if (!request.VoucherHasCombinedUsageLimit)
            {
                request.VoucherMaxCombinedUsageCount = null;
            }
            else if (!request.VoucherMaxCombinedUsageCount.HasValue || request.VoucherMaxCombinedUsageCount.Value < 1)
            {
                ModelState.AddModelError(nameof(request.VoucherMaxCombinedUsageCount), "Vui lòng nhập số voucher áp dụng chung tối đa hợp lệ.");
            }

            if (request.VoucherQuantity < 1)
            {
                ModelState.AddModelError(nameof(request.VoucherQuantity), "Số lượng vé giảm giá phải lớn hơn hoặc bằng 1.");
            }

            if (request.VoucherDiscountType == VoucherDiscountType.Percent)
            {
                if (request.VoucherDiscount > 100)
                {
                    ModelState.AddModelError(nameof(request.VoucherDiscount), "Phần trăm giảm giá tối đa là 100%.");
                }

                if (!request.VoucherUnlimitedPercentageDiscount)
                {
                    if (!request.VoucherMaximumPercentageReduction.HasValue || request.VoucherMaximumPercentageReduction.Value <= 0)
                    {
                        ModelState.AddModelError(nameof(request.VoucherMaximumPercentageReduction), "Vui lòng nhập mức giảm tối đa hợp lệ.");
                    }
                }
                else
                {
                    request.VoucherMaximumPercentageReduction = null;
                }
            }
            else
            {
                request.VoucherUnlimitedPercentageDiscount = false;
                request.VoucherMaximumPercentageReduction = null;
            }

            if (request.VoucherProductScope == VoucherProductScope.NoProducts && request.VoucherComboScope == VoucherComboScope.NoCombos)
            {
                ModelState.AddModelError(nameof(request.VoucherProductScope), "Vật phẩm đổi thưởng phải áp dụng cho ít nhất sản phẩm hoặc combo.");
            }
        }

        private async Task ValidateRewardScopeSelectionsAsync(RewardRequest request)
        {
            request.ProductIds = request.ProductIds?.Where(id => id > 0).Distinct().ToList() ?? new List<long>();
            request.ComboIds = request.ComboIds?.Where(id => id > 0).Distinct().ToList() ?? new List<long>();

            if (request.VoucherProductScope != VoucherProductScope.SelectedProducts)
            {
                request.ProductIds.Clear();
            }

            if (request.VoucherComboScope != VoucherComboScope.SelectedCombos)
            {
                request.ComboIds.Clear();
            }

            if (request.VoucherProductScope == VoucherProductScope.SelectedProducts)
            {
                if (!request.ProductIds.Any())
                {
                    ModelState.AddModelError(nameof(request.ProductIds), "Vui lòng chọn ít nhất một sản phẩm áp dụng.");
                }
                else
                {
                    var existingProductIds = await _context.Products
                        .Where(p => !p.IsDeleted && request.ProductIds.Contains(p.Id))
                        .Select(p => p.Id)
                        .ToListAsync();

                    var missingIds = request.ProductIds.Except(existingProductIds).ToList();
                    if (missingIds.Any())
                    {
                        ModelState.AddModelError(nameof(request.ProductIds), "Một số sản phẩm đã chọn không tồn tại hoặc đã bị xóa.");
                    }
                    else
                    {
                        request.ProductIds = existingProductIds;
                    }
                }
            }

            if (request.VoucherComboScope == VoucherComboScope.SelectedCombos)
            {
                if (!request.ComboIds.Any())
                {
                    ModelState.AddModelError(nameof(request.ComboIds), "Vui lòng chọn ít nhất một combo áp dụng.");
                }
                else
                {
                    var existingComboIds = await _context.Combos
                        .Where(c => !c.IsDeleted && request.ComboIds.Contains(c.Id))
                        .Select(c => c.Id)
                        .ToListAsync();

                    var missingComboIds = request.ComboIds.Except(existingComboIds).ToList();
                    if (missingComboIds.Any())
                    {
                        ModelState.AddModelError(nameof(request.ComboIds), "Một số combo đã chọn không tồn tại hoặc đã bị xóa.");
                    }
                    else
                    {
                        request.ComboIds = existingComboIds;
                    }
                }
            }
        }

        private async Task UpdateRewardAssociationsAsync(Reward reward, RewardRequest request, DateTime timestamp)
        {
            if (reward == null)
            {
                return;
            }

            var now = timestamp == default ? DateTime.Now : timestamp;
            var currentUserId = CurrentUserId;

            var existingRewardProducts = await _context.RewardProducts
                .Where(rp => rp.RewardId == reward.Id && !rp.IsDeleted)
                .ToListAsync();

            if (request.VoucherProductScope != VoucherProductScope.SelectedProducts || !request.ProductIds.Any())
            {
                foreach (var rewardProduct in existingRewardProducts)
                {
                    rewardProduct.IsDeleted = true;
                    rewardProduct.DeletedAt = now;
                    rewardProduct.UpdatedAt = now;
                }
            }
            else
            {
                var desiredProductIds = new HashSet<long>(request.ProductIds);
                foreach (var rewardProduct in existingRewardProducts)
                {
                    if (!desiredProductIds.Contains(rewardProduct.ProductId))
                    {
                        rewardProduct.IsDeleted = true;
                        rewardProduct.DeletedAt = now;
                        rewardProduct.UpdatedAt = now;
                    }
                    else
                    {
                        desiredProductIds.Remove(rewardProduct.ProductId);
                    }
                }

                foreach (var productId in desiredProductIds)
                {
                    var newRewardProduct = new RewardProduct
                    {
                        RewardId = reward.Id,
                        ProductId = productId,
                        CreateBy = currentUserId,
                        CreatedAt = now,
                        UpdatedAt = null,
                        IsDeleted = false,
                        DeletedAt = null
                    };

                    _context.RewardProducts.Add(newRewardProduct);
                }
            }

            var existingRewardCombos = await _context.RewardCombos
                .Where(rc => rc.RewardId == reward.Id && !rc.IsDeleted)
                .ToListAsync();

            if (request.VoucherComboScope != VoucherComboScope.SelectedCombos || !request.ComboIds.Any())
            {
                foreach (var rewardCombo in existingRewardCombos)
                {
                    rewardCombo.IsDeleted = true;
                    rewardCombo.DeletedAt = now;
                    rewardCombo.UpdatedAt = now;
                }
            }
            else
            {
                var desiredComboIds = new HashSet<long>(request.ComboIds);
                foreach (var rewardCombo in existingRewardCombos)
                {
                    if (!desiredComboIds.Contains(rewardCombo.ComboId))
                    {
                        rewardCombo.IsDeleted = true;
                        rewardCombo.DeletedAt = now;
                        rewardCombo.UpdatedAt = now;
                    }
                    else
                    {
                        desiredComboIds.Remove(rewardCombo.ComboId);
                    }
                }

                foreach (var comboId in desiredComboIds)
                {
                    var newRewardCombo = new RewardCombo
                    {
                        RewardId = reward.Id,
                        ComboId = comboId,
                        CreateBy = currentUserId,
                        CreatedAt = now,
                        UpdatedAt = null,
                        IsDeleted = false,
                        DeletedAt = null
                    };

                    _context.RewardCombos.Add(newRewardCombo);
                }
            }
        }

        private static RewardListItem MapToListItem(Reward reward) => new()
        {
            Id = reward.Id,
            Type = reward.Type,
            Name = reward.Name,
            Description = reward.Description,
            PointCost = reward.PointCost,
            MinimumRank = reward.MinimumRank,
            Quantity = reward.Quantity,
            IsQuantityUnlimited = reward.IsQuantityUnlimited,
            Redeemed = reward.Redeemed,
            ValidityValue = reward.ValidityValue,
            ValidityUnit = reward.ValidityUnit,
            IsValidityUnlimited = reward.IsValidityUnlimited,
            IsPublish = reward.IsPublish,
            CreatedAt = reward.CreatedAt,
            UpdatedAt = reward.UpdatedAt,
            VoucherProductScope = reward.VoucherProductScope,
            VoucherComboScope = reward.VoucherComboScope,
            VoucherDiscountType = reward.VoucherDiscountType,
            VoucherDiscount = reward.VoucherDiscount,
            VoucherMinimumRequirements = reward.VoucherMinimumRequirements,
            VoucherUnlimitedPercentageDiscount = reward.VoucherUnlimitedPercentageDiscount,
            VoucherMaximumPercentageReduction = reward.VoucherMaximumPercentageReduction,
            VoucherHasCombinedUsageLimit = reward.VoucherHasCombinedUsageLimit,
            VoucherMaxCombinedUsageCount = reward.VoucherMaxCombinedUsageCount,
            VoucherIsForNewUsersOnly = reward.VoucherIsForNewUsersOnly,
            VoucherQuantity = reward.VoucherQuantity,
            SelectedProductCount = reward.RewardProducts?.Count(rp => !rp.IsDeleted) ?? 0,
            SelectedComboCount = reward.RewardCombos?.Count(rc => !rc.IsDeleted) ?? 0
        };

        private static RewardDetail MapToDetail(Reward reward) => new()
        {
            Id = reward.Id,
            Type = reward.Type,
            Name = reward.Name,
            Description = reward.Description,
            PointCost = reward.PointCost,
            MinimumRank = reward.MinimumRank,
            Quantity = reward.Quantity,
            IsQuantityUnlimited = reward.IsQuantityUnlimited,
            Redeemed = reward.Redeemed,
            ValidityValue = reward.ValidityValue,
            ValidityUnit = reward.ValidityUnit,
            IsValidityUnlimited = reward.IsValidityUnlimited,
            IsPublish = reward.IsPublish,
            CreatedAt = reward.CreatedAt,
            UpdatedAt = reward.UpdatedAt,
            VoucherProductScope = reward.VoucherProductScope,
            VoucherComboScope = reward.VoucherComboScope,
            VoucherDiscountType = reward.VoucherDiscountType,
            VoucherDiscount = reward.VoucherDiscount,
            VoucherMinimumRequirements = reward.VoucherMinimumRequirements,
            VoucherUnlimitedPercentageDiscount = reward.VoucherUnlimitedPercentageDiscount,
            VoucherMaximumPercentageReduction = reward.VoucherMaximumPercentageReduction,
            VoucherHasCombinedUsageLimit = reward.VoucherHasCombinedUsageLimit,
            VoucherMaxCombinedUsageCount = reward.VoucherMaxCombinedUsageCount,
            VoucherIsForNewUsersOnly = reward.VoucherIsForNewUsersOnly,
            VoucherQuantity = reward.VoucherQuantity,
            SelectedProductCount = reward.RewardProducts?.Count(rp => !rp.IsDeleted) ?? 0,
            SelectedComboCount = reward.RewardCombos?.Count(rc => !rc.IsDeleted) ?? 0,
            SelectedProducts = reward.RewardProducts?
                .Where(rp => !rp.IsDeleted)
                .Select(rp => new RewardScopeItem
                {
                    Id = rp.ProductId,
                    Name = rp.Product?.Name ?? $"Sản phẩm #{rp.ProductId}"
                })
                .OrderBy(item => item.Name)
                .ToList() ?? new List<RewardScopeItem>(),
            SelectedCombos = reward.RewardCombos?
                .Where(rc => !rc.IsDeleted)
                .Select(rc => new RewardScopeItem
                {
                    Id = rc.ComboId,
                    Name = rc.Combo?.Name ?? $"Combo #{rc.ComboId}"
                })
                .OrderBy(item => item.Name)
                .ToList() ?? new List<RewardScopeItem>()
        };

        public class RewardRequest
        {
            public RewardItemType Type { get; set; } = RewardItemType.Voucher;

            [Required]
            [StringLength(300)]
            public string? Name { get; set; }

            [Required]
            [StringLength(1000)]
            public string? Description { get; set; }

            [Range(0, long.MaxValue)]
            public long PointCost { get; set; }

            public CustomerRank? MinimumRank { get; set; }

            [Range(0, long.MaxValue)]
            public long Quantity { get; set; }

            [Range(0, int.MaxValue)]
            public int ValidityValue { get; set; }

            public RewardValidityUnit ValidityUnit { get; set; }

            public bool IsValidityUnlimited { get; set; }

            public bool IsQuantityUnlimited { get; set; }

            public bool IsPublish { get; set; }

            public VoucherProductScope VoucherProductScope { get; set; }

            public VoucherComboScope VoucherComboScope { get; set; }

            [Range(0, double.MaxValue)]
            public double VoucherDiscount { get; set; }

            public VoucherDiscountType VoucherDiscountType { get; set; }

            [Range(0, double.MaxValue)]
            public double VoucherMinimumRequirements { get; set; }

            public bool VoucherUnlimitedPercentageDiscount { get; set; }

            [Range(0, double.MaxValue)]
            public double? VoucherMaximumPercentageReduction { get; set; }

            public bool VoucherHasCombinedUsageLimit { get; set; }

            [Range(1, int.MaxValue)]
            public int? VoucherMaxCombinedUsageCount { get; set; }

            public bool VoucherIsForNewUsersOnly { get; set; }

            [Range(1, int.MaxValue)]
            public int VoucherQuantity { get; set; } = 1;

            public List<long> ProductIds { get; set; } = new();

            public List<long> ComboIds { get; set; } = new();
        }

        public class RewardListItem
        {
            public long Id { get; set; }
            public RewardItemType Type { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public long PointCost { get; set; }
            public CustomerRank? MinimumRank { get; set; }
            public long Quantity { get; set; }
            public bool IsQuantityUnlimited { get; set; }
            public long Redeemed { get; set; }
            public int ValidityValue { get; set; }
            public RewardValidityUnit ValidityUnit { get; set; }
            public bool IsValidityUnlimited { get; set; }
            public bool IsPublish { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? UpdatedAt { get; set; }
            public VoucherProductScope VoucherProductScope { get; set; }
            public VoucherComboScope VoucherComboScope { get; set; }
            public VoucherDiscountType VoucherDiscountType { get; set; }
            public double VoucherDiscount { get; set; }
            public double VoucherMinimumRequirements { get; set; }
            public bool VoucherUnlimitedPercentageDiscount { get; set; }
            public double? VoucherMaximumPercentageReduction { get; set; }
            public bool VoucherHasCombinedUsageLimit { get; set; }
            public int? VoucherMaxCombinedUsageCount { get; set; }
            public bool VoucherIsForNewUsersOnly { get; set; }
            public int VoucherQuantity { get; set; }
            public int SelectedProductCount { get; set; }
            public int SelectedComboCount { get; set; }
        }

        public class RewardDetail : RewardListItem
        {
            public IReadOnlyCollection<RewardScopeItem> SelectedProducts { get; set; } = Array.Empty<RewardScopeItem>();
            public IReadOnlyCollection<RewardScopeItem> SelectedCombos { get; set; } = Array.Empty<RewardScopeItem>();
        }

        public class RewardScopeItem
        {
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public class FormOptionsResponse
        {
            public IReadOnlyCollection<ProductOption> Products { get; set; } = Array.Empty<ProductOption>();
            public IReadOnlyCollection<ComboOption> Combos { get; set; } = Array.Empty<ComboOption>();
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

        public class BulkPublishRequest
        {
            public List<long> Ids { get; set; } = new();
            public bool IsPublish { get; set; }
        }
    }
}
