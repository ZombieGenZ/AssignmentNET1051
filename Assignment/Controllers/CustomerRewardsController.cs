using System;
using System.Collections.Generic;
using System.Linq;
using Assignment.Data;
using Assignment.Enums;
using Assignment.Models;
using Assignment.Services;
using Assignment.ViewModels.Rewards;
using Assignment.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Assignment.Controllers
{
    [Authorize]
    public class CustomerRewardsController : Controller
    {
        private static readonly int[] PageSizeOptions = { 6, 12, 18, 24 };
        private const int DefaultPageSize = 12;

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CustomerRewardsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, int pageSize = DefaultPageSize)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            page = NormalizePage(page);
            pageSize = NormalizePageSize(pageSize);

            IQueryable<Reward> query = _context.Rewards
                .AsNoTracking()
                .Where(r => !r.IsDeleted && r.IsPublish)
                .Where(r => !r.MinimumRank.HasValue || user.Rank >= r.MinimumRank.Value)
                .Where(r => r.IsQuantityUnlimited || r.Quantity <= 0 || r.Redeemed < r.Quantity);

            var totalItems = await query.CountAsync();
            var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
            if (totalPages > 0 && page > totalPages)
            {
                page = totalPages;
            }

            var rewards = await query
                .OrderBy(r => r.PointCost)
                .ThenBy(r => r.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var rewardItems = rewards
                .Select(r => new CustomerRewardItemViewModel
                {
                    Id = r.Id,
                    Name = r.Name,
                    Description = r.Description,
                    PointCost = r.PointCost,
                    MinimumRank = r.MinimumRank,
                    Quantity = r.Quantity,
                    Redeemed = r.Redeemed,
                    ValidityValue = r.ValidityValue,
                    ValidityUnit = r.ValidityUnit,
                    IsPublish = r.IsPublish,
                    IsAvailable = r.IsQuantityUnlimited || r.Quantity <= 0 || r.Redeemed < r.Quantity,
                    IsValidityUnlimited = r.IsValidityUnlimited,
                    IsQuantityUnlimited = r.IsQuantityUnlimited,
                    VoucherQuantity = r.VoucherQuantity
                })
                .ToList();

            var pagedResult = new PagedResult<CustomerRewardItemViewModel>
            {
                CurrentPage = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                PageSizeOptions = PageSizeOptions
            };
            pagedResult.SetItems(rewardItems);
            pagedResult.EnsureValidPage();

            var (nextRank, requiredExp) = CustomerRankCalculator.GetNextRankInfo(user.Exp);

            var viewModel = new CustomerRewardIndexViewModel
            {
                Rewards = pagedResult,
                CurrentPoint = user.Point,
                TotalPoint = user.TotalPoint,
                Exp = user.Exp,
                Rank = user.Rank,
                NextRank = nextRank,
                NextRankExp = requiredExp
            };

            ViewData["Title"] = "Đổi thưởng";

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Redeem(long id, int? page, int? pageSize, int quantity = 1)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var reward = await _context.Rewards
                .Include(r => r.RewardProducts.Where(rp => !rp.IsDeleted))
                    .ThenInclude(rp => rp.Product)
                .Include(r => r.RewardCombos.Where(rc => !rc.IsDeleted))
                    .ThenInclude(rc => rc.Combo)
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
            if (reward == null || !reward.IsPublish)
            {
                TempData["RewardError"] = "Vật phẩm đổi thưởng không khả dụng.";
                return RedirectToIndex(page, pageSize);
            }

            if (reward.MinimumRank.HasValue && user.Rank < reward.MinimumRank.Value)
            {
                TempData["RewardError"] = "Bạn chưa đạt cấp bậc để đổi vật phẩm này.";
                return RedirectToIndex(page, pageSize);
            }

            var sanitizedQuantity = quantity < 1 ? 1 : quantity;
            var sanitizedQuantityLong = (long)sanitizedQuantity;

            if (!reward.IsQuantityUnlimited && reward.Quantity > 0 && reward.Redeemed >= reward.Quantity)
            {
                TempData["RewardError"] = "Vật phẩm đã hết số lượng.";
                return RedirectToIndex(page, pageSize);
            }

            if (!reward.IsQuantityUnlimited && reward.Quantity > 0 && reward.Redeemed + sanitizedQuantityLong > reward.Quantity)
            {
                TempData["RewardError"] = "Số lượng yêu cầu vượt quá số lượng còn lại.";
                return RedirectToIndex(page, pageSize);
            }

            var totalPointCost = reward.PointCost * sanitizedQuantityLong;

            if (user.Point < totalPointCost)
            {
                TempData["RewardError"] = "Điểm thưởng của bạn không đủ để đổi vật phẩm này.";
                return RedirectToIndex(page, pageSize);
            }

            var now = DateTime.Now;
            var validFrom = now;
            var validTo = CalculateExpiry(now, reward.ValidityValue, reward.ValidityUnit, reward.IsValidityUnlimited);

            try
            {
                var generatedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var rewardProductIds = reward.RewardProducts?.Where(rp => !rp.IsDeleted).Select(rp => rp.ProductId).ToList() ?? new List<long>();
                var rewardComboIds = reward.RewardCombos?.Where(rc => !rc.IsDeleted).Select(rc => rc.ComboId).ToList() ?? new List<long>();
                var createdCodes = new List<string>();

                for (var i = 0; i < sanitizedQuantity; i++)
                {
                    var code = await GenerateUniqueCodeAsync(generatedCodes);
                    generatedCodes.Add(code);
                    createdCodes.Add(code);

                    var voucher = new Voucher
                    {
                        Code = code,
                        Name = reward.Name,
                        Description = reward.Description,
                        Type = VoucherType.Private,
                        ProductScope = reward.VoucherProductScope,
                        ComboScope = reward.VoucherComboScope,
                        UserId = user.Id,
                        Discount = reward.VoucherDiscount,
                        DiscountType = reward.VoucherDiscountType,
                        Used = 0,
                        Quantity = reward.VoucherQuantity,
                        StartTime = validFrom,
                        IsLifeTime = reward.IsValidityUnlimited,
                        EndTime = reward.IsValidityUnlimited ? null : validTo,
                        MinimumRequirements = reward.VoucherMinimumRequirements,
                        UnlimitedPercentageDiscount = reward.VoucherDiscountType == VoucherDiscountType.Percent && reward.VoucherUnlimitedPercentageDiscount,
                        MaximumPercentageReduction = reward.VoucherDiscountType == VoucherDiscountType.Percent && !reward.VoucherUnlimitedPercentageDiscount
                            ? reward.VoucherMaximumPercentageReduction
                            : null,
                        HasCombinedUsageLimit = reward.VoucherHasCombinedUsageLimit,
                        MaxCombinedUsageCount = reward.VoucherHasCombinedUsageLimit ? reward.VoucherMaxCombinedUsageCount : null,
                        IsPublish = true,
                        IsShow = false,
                        IsForNewUsersOnly = reward.VoucherIsForNewUsersOnly,
                        MinimumRank = null,
                        CreateBy = user.Id,
                        CreatedAt = now,
                        UpdatedAt = null,
                        IsDeleted = false,
                        DeletedAt = null,
                        VoucherProducts = rewardProductIds.Select(productId => new VoucherProduct
                        {
                            ProductId = productId,
                            CreateBy = user.Id,
                            CreatedAt = now,
                            UpdatedAt = null,
                            IsDeleted = false,
                            DeletedAt = null
                        }).ToList(),
                        VoucherCombos = rewardComboIds.Select(comboId => new VoucherCombo
                        {
                            ComboId = comboId,
                            CreateBy = user.Id,
                            CreatedAt = now,
                            UpdatedAt = null,
                            IsDeleted = false,
                            DeletedAt = null
                        }).ToList()
                    };

                    var redemption = new RewardRedemption
                    {
                        RewardId = reward.Id,
                        UserId = user.Id,
                        Code = code,
                        ValidFrom = validFrom,
                        ValidTo = validTo,
                        IsUsed = false,
                        UsedAt = null,
                        PointCost = reward.PointCost,
                        Voucher = voucher,
                        CreateBy = user.Id,
                        CreatedAt = now,
                        UpdatedAt = null,
                        IsDeleted = false,
                        DeletedAt = null
                    };

                    _context.Vouchers.Add(voucher);
                    _context.RewardRedemptions.Add(redemption);
                }

                reward.Redeemed += sanitizedQuantityLong;
                reward.UpdatedAt = now;

                user.Point = Math.Max(0, user.Point - totalPointCost);

                _context.Rewards.Update(reward);
                _context.Users.Update(user);

                await _context.SaveChangesAsync();

                TempData["RewardSuccess"] = sanitizedQuantity > 1
                    ? $"Bạn đã đổi thành công {sanitizedQuantity} vật phẩm \"{reward.Name}\"."
                    : $"Bạn đã đổi thành công \"{reward.Name}\".";
                TempData["RewardCodes"] = JsonSerializer.Serialize(createdCodes);
                TempData["RewardCode"] = createdCodes.FirstOrDefault();
                TempData["RewardValidFrom"] = validFrom.ToString("o");
                TempData["RewardValidTo"] = validTo?.ToString("o");
            }
            catch (DbUpdateException)
            {
                TempData["RewardError"] = "Không thể đổi vật phẩm. Vui lòng thử lại sau.";
            }

            return RedirectToIndex(page, pageSize);
        }

        private IActionResult RedirectToIndex(int? page, int? pageSize)
        {
            var sanitizedPage = page.HasValue ? NormalizePage(page.Value) : 1;
            var sanitizedPageSize = pageSize.HasValue ? NormalizePageSize(pageSize.Value) : DefaultPageSize;
            return RedirectToAction(nameof(Index), new { page = sanitizedPage, pageSize = sanitizedPageSize });
        }

        private static int NormalizePage(int page)
        {
            return page < 1 ? 1 : page;
        }

        private static int NormalizePageSize(int pageSize)
        {
            return PageSizeOptions.Contains(pageSize) ? pageSize : DefaultPageSize;
        }

        private async Task<string> GenerateUniqueCodeAsync(ISet<string>? reservedCodes = null)
        {
            const int maxAttempts = 10;

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                var randomSegment = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
                var code = $"RW-{randomSegment}";

                if (reservedCodes != null && reservedCodes.Contains(code))
                {
                    continue;
                }

                var exists = await _context.RewardRedemptions
                    .AsNoTracking()
                    .AnyAsync(r => r.Code == code);

                if (!exists)
                {
                    return code;
                }
            }

            throw new InvalidOperationException("Không thể tạo mã đổi thưởng duy nhất. Vui lòng thử lại sau.");
        }

        private static DateTime? CalculateExpiry(DateTime start, int validityValue, RewardValidityUnit unit, bool isUnlimited)
        {
            if (isUnlimited || unit == RewardValidityUnit.Forever)
            {
                return null;
            }

            var sanitizedValue = validityValue < 1 ? 1 : validityValue;

            return unit switch
            {
                RewardValidityUnit.Minute => start.AddMinutes(sanitizedValue),
                RewardValidityUnit.Week => start.AddDays(7 * sanitizedValue),
                RewardValidityUnit.Month => start.AddMonths(sanitizedValue),
                RewardValidityUnit.Year => start.AddYears(sanitizedValue),
                _ => start.AddDays(sanitizedValue)
            };
        }
    }
}
