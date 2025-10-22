using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Assignment.Data;
using Assignment.Enums;
using Assignment.Models;
using Assignment.Options;
using Assignment.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

            IQueryable<Reward> query = _context.Rewards
                .AsNoTracking()
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
            var reward = await _context.Rewards.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
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
            ValidateRewardRequest(request);
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var now = DateTime.Now;
            var reward = new Reward
            {
                Name = request.Name!.Trim(),
                Description = request.Description!.Trim(),
                PointCost = request.PointCost,
                MinimumRank = request.MinimumRank,
                Quantity = request.Quantity,
                Redeemed = 0,
                ValidityValue = request.ValidityValue,
                ValidityUnit = request.ValidityUnit,
                IsPublish = request.IsPublish,
                CreateBy = CurrentUserId,
                CreatedAt = now,
                UpdatedAt = null,
                DeletedAt = null,
                IsDeleted = false
            };

            _context.Rewards.Add(reward);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetReward), new { id = reward.Id }, MapToDetail(reward));
        }

        [HttpPut("{id:long}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateReward(long id, [FromBody] RewardRequest request)
        {
            var reward = await _context.Rewards.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
            if (reward == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, reward, "UpdateRewardPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            ValidateRewardRequest(request);
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            reward.Name = request.Name!.Trim();
            reward.Description = request.Description!.Trim();
            reward.PointCost = request.PointCost;
            reward.MinimumRank = request.MinimumRank;
            reward.Quantity = request.Quantity;
            reward.ValidityValue = request.ValidityValue;
            reward.ValidityUnit = request.ValidityUnit;
            reward.IsPublish = request.IsPublish;
            reward.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return Ok(MapToDetail(reward));
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

        private void ValidateRewardRequest(RewardRequest request)
        {
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

            if (request.Quantity < 0)
            {
                ModelState.AddModelError(nameof(request.Quantity), "Số lượng phải lớn hơn hoặc bằng 0.");
            }

            if (request.ValidityValue < 1)
            {
                ModelState.AddModelError(nameof(request.ValidityValue), "Thời hạn hiệu lực phải lớn hơn hoặc bằng 1.");
            }
        }

        private static RewardListItem MapToListItem(Reward reward) => new()
        {
            Id = reward.Id,
            Name = reward.Name,
            Description = reward.Description,
            PointCost = reward.PointCost,
            MinimumRank = reward.MinimumRank,
            Quantity = reward.Quantity,
            Redeemed = reward.Redeemed,
            ValidityValue = reward.ValidityValue,
            ValidityUnit = reward.ValidityUnit,
            IsPublish = reward.IsPublish,
            CreatedAt = reward.CreatedAt,
            UpdatedAt = reward.UpdatedAt
        };

        private static RewardDetail MapToDetail(Reward reward) => new()
        {
            Id = reward.Id,
            Name = reward.Name,
            Description = reward.Description,
            PointCost = reward.PointCost,
            MinimumRank = reward.MinimumRank,
            Quantity = reward.Quantity,
            Redeemed = reward.Redeemed,
            ValidityValue = reward.ValidityValue,
            ValidityUnit = reward.ValidityUnit,
            IsPublish = reward.IsPublish,
            CreatedAt = reward.CreatedAt,
            UpdatedAt = reward.UpdatedAt
        };

        public class RewardRequest
        {
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

            [Range(1, int.MaxValue)]
            public int ValidityValue { get; set; }

            public RewardValidityUnit ValidityUnit { get; set; }

            public bool IsPublish { get; set; }
        }

        public class RewardListItem
        {
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public long PointCost { get; set; }
            public CustomerRank? MinimumRank { get; set; }
            public long Quantity { get; set; }
            public long Redeemed { get; set; }
            public int ValidityValue { get; set; }
            public RewardValidityUnit ValidityUnit { get; set; }
            public bool IsPublish { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? UpdatedAt { get; set; }
        }

        public class RewardDetail : RewardListItem
        {
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
