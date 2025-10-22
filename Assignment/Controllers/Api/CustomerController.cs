using System.Security.Claims;
using Assignment.Data;
using Assignment.Enums;
using Assignment.Models;
using Assignment.Options;
using Assignment.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Assignment.Controllers.Api
{
    [ApiController]
    [Route("api/customers")]
    [Authorize]
    public class CustomerController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CustomerController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> GetCustomers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = PaginationDefaults.DefaultPageSize,
            [FromQuery] string? search = null)
        {
            if (!User.HasPermission("ViewCustomerAll"))
            {
                return Forbid();
            }

            page = PaginationDefaults.NormalizePage(page);
            pageSize = PaginationDefaults.NormalizePageSize(pageSize);

            IQueryable<ApplicationUser> query = _userManager.Users.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                var like = $"%{term}%";
                query = query.Where(u =>
                    EF.Functions.Like(u.FullName ?? string.Empty, like) ||
                    EF.Functions.Like(u.Email ?? string.Empty, like) ||
                    EF.Functions.Like(u.UserName ?? string.Empty, like));
            }

            var totalItems = await query.CountAsync();
            var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
            if (totalPages > 0 && page > totalPages)
            {
                page = totalPages;
            }

            var users = await query
                .OrderBy(u => u.FullName ?? u.Email ?? u.UserName ?? u.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = users.Select(user => new CustomerListItem
            {
                Id = user.Id,
                FullName = user.FullName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                Phone = user.PhoneNumber ?? string.Empty,
                Exp = user.Exp,
                Point = user.Point,
                TotalPoint = user.TotalPoint,
                Rank = user.Rank
            }).ToList();

            var response = new PagedResponse<CustomerListItem>
            {
                CurrentPage = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = totalPages,
                PageSizeOptions = PaginationDefaults.PageSizeOptions.ToArray(),
                Items = items
            };

            return Ok(response);
        }

        [HttpGet("top")]
        public async Task<IActionResult> GetTopCustomers(
            [FromQuery] string period = "month",
            [FromQuery] int? month = null,
            [FromQuery] int? quarter = null,
            [FromQuery] int? year = null)
        {
            if (!User.HasPermission("ViewTopUserAll"))
            {
                return Forbid();
            }

            var now = DateTime.Now;
            var targetYear = year ?? now.Year;
            DateTime start;
            DateTime end;

            switch (period?.ToLowerInvariant())
            {
                case "quarter":
                    var targetQuarter = quarter.HasValue && quarter.Value is >= 1 and <= 4
                        ? quarter.Value
                        : (now.Month - 1) / 3 + 1;
                    start = new DateTime(targetYear, (targetQuarter - 1) * 3 + 1, 1);
                    end = start.AddMonths(3);
                    break;
                case "year":
                    start = new DateTime(targetYear, 1, 1);
                    end = start.AddYears(1);
                    break;
                default:
                    var targetMonth = month.HasValue && month.Value is >= 1 and <= 12
                        ? month.Value
                        : now.Month;
                    start = new DateTime(targetYear, targetMonth, 1);
                    end = start.AddMonths(1);
                    break;
            }

            var validStatuses = new[] { OrderStatus.Paid, OrderStatus.Completed };

            var orders = await _context.Orders
                .AsNoTracking()
                .Where(o => !o.IsDeleted && o.UserId != null && validStatuses.Contains(o.Status))
                .Where(o => o.CreatedAt >= start && o.CreatedAt < end)
                .GroupBy(o => o.UserId!)
                .Select(group => new
                {
                    UserId = group.Key,
                    OrderCount = group.Count(),
                    TotalAmount = group.Sum(o => o.TotalBill)
                })
                .ToListAsync();

            var redemptions = await _context.RewardRedemptions
                .AsNoTracking()
                .Where(r => !r.IsDeleted && r.UserId != null)
                .Where(r => r.CreatedAt >= start && r.CreatedAt < end)
                .GroupBy(r => r.UserId)
                .Select(group => new
                {
                    UserId = group.Key,
                    TotalPoints = group.Sum(r => r.PointCost)
                })
                .ToListAsync();

            var orderLookup = orders.ToDictionary(o => o.UserId, o => o);
            var redemptionLookup = redemptions.ToDictionary(r => r.UserId, r => r.TotalPoints);

            var userIds = orderLookup.Keys.Union(redemptionLookup.Keys).ToList();
            if (!userIds.Any())
            {
                return Ok(new List<TopCustomerItem>());
            }

            var users = await _userManager.Users
                .AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToListAsync();

            var userMap = users.ToDictionary(u => u.Id, u => u);

            var topCustomers = new List<TopCustomerItem>();
            foreach (var userId in userIds)
            {
                if (!userMap.TryGetValue(userId, out var user))
                {
                    continue;
                }

                var orderInfo = orderLookup.TryGetValue(userId, out var info) ? info : null;
                var pointsRedeemed = redemptionLookup.TryGetValue(userId, out var redeemed) ? redeemed : 0;
                var rawAmount = orderInfo?.TotalAmount ?? 0;
                var baseValue = (long)Math.Round(rawAmount, MidpointRounding.AwayFromZero);

                topCustomers.Add(new TopCustomerItem
                {
                    UserId = userId,
                    FullName = user.FullName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    Rank = user.Rank,
                    ExpEarned = baseValue * 2,
                    PointsEarned = baseValue,
                    PointsRedeemed = pointsRedeemed,
                    TotalPoint = user.TotalPoint,
                    OrderCount = orderInfo?.OrderCount ?? 0
                });
            }

            var ordered = topCustomers
                .OrderByDescending(c => c.ExpEarned)
                .ThenByDescending(c => c.PointsEarned)
                .ThenBy(c => c.FullName)
                .Take(20)
                .ToList();

            return Ok(ordered);
        }

        private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

        public class CustomerListItem
        {
            public string Id { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public long Exp { get; set; }
            public long Point { get; set; }
            public long TotalPoint { get; set; }
            public CustomerRank Rank { get; set; }
        }

        public class TopCustomerItem
        {
            public string UserId { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public CustomerRank Rank { get; set; }
            public long ExpEarned { get; set; }
            public long PointsEarned { get; set; }
            public long PointsRedeemed { get; set; }
            public long TotalPoint { get; set; }
            public int OrderCount { get; set; }
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
    }
}
