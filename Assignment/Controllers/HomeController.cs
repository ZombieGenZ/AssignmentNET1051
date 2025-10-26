using Assignment.Data;
using Assignment.Enums;
using Assignment.Models;
using Assignment.Services;
using Assignment.ViewModels;
using Assignment.ViewModels.Combos;
using Assignment.ViewModels.Products;
using Assignment.ViewModels.Ratings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;

namespace Assignment.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index([FromQuery] HomeFilterViewModel? filter)
        {
            filter ??= new HomeFilterViewModel();
            filter.Normalize();

            if (filter.CategoryId.HasValue && filter.Segment != "combo")
            {
                filter.Segment = "category";
            }

            if (filter.Segment == "category" && !filter.CategoryId.HasValue)
            {
                filter.Segment = "all";
            }

            if (filter.MinPrice.HasValue && filter.MaxPrice.HasValue && filter.MinPrice > filter.MaxPrice)
            {
                (filter.MinPrice, filter.MaxPrice) = (filter.MaxPrice, filter.MinPrice);
            }

            var products = await _context.Products
                .Where(p => !p.IsDeleted)
                .Include(p => p.ProductTypes)
                .Include(p => p.Category)
                .OrderBy(p => p.Category.Index)
                .ThenBy(p => p.Id)
                .ToListAsync();

            foreach (var product in products)
            {
                product.RefreshDerivedFields();
            }

            products = products
                .Where(p => p.IsPublish)
                .ToList();

            var combos = await _context.Combos
                .Include(c => c.ComboItems)
                    .ThenInclude(ci => ci.Product)
                        .ThenInclude(p => p.ProductTypes)
                .Where(c => !c.IsDeleted && c.IsPublish)
                .OrderBy(c => c.Index)
                .ToListAsync();

            foreach (var combo in combos)
            {
                combo.RefreshDerivedFields();
            }

            var categories = await _context.Categories
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.Index)
                .ToListAsync();

            IEnumerable<Product> filteredProducts = products;
            IEnumerable<Combo> filteredCombos = combos;

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var term = filter.SearchTerm.Trim();
                filteredProducts = filteredProducts.Where(p =>
                    p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    p.Description.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (p.Category != null && p.Category.Name.Contains(term, StringComparison.OrdinalIgnoreCase)));

                filteredCombos = filteredCombos.Where(c =>
                    c.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    c.Description.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            if (filter.IsSpicy.HasValue)
            {
                if (filter.IsSpicy.Value)
                {
                    filteredProducts = filteredProducts.Where(p => p.IsSpicy);
                    filteredCombos = filteredCombos.Where(c =>
                        (c.ComboItems ?? Enumerable.Empty<ComboItem>()).Any(ci => ci.Product?.IsSpicy == true));
                }
                else
                {
                    filteredProducts = filteredProducts.Where(p => !p.IsSpicy);
                    filteredCombos = filteredCombos.Where(c =>
                        (c.ComboItems ?? Enumerable.Empty<ComboItem>()).All(ci => ci.Product?.IsSpicy != true));
                }
            }

            if (filter.IsVegetarian.HasValue)
            {
                if (filter.IsVegetarian.Value)
                {
                    filteredProducts = filteredProducts.Where(p => p.IsVegetarian);
                    filteredCombos = filteredCombos.Where(c =>
                        (c.ComboItems ?? Enumerable.Empty<ComboItem>()).All(ci => ci.Product?.IsVegetarian == true));
                }
                else
                {
                    filteredProducts = filteredProducts.Where(p => !p.IsVegetarian);
                    filteredCombos = filteredCombos.Where(c =>
                        (c.ComboItems ?? Enumerable.Empty<ComboItem>()).Any(ci => ci.Product?.IsVegetarian == false));
                }
            }

            if (filter.MinPrice.HasValue)
            {
                var min = (double)filter.MinPrice.Value;
                filteredProducts = filteredProducts.Where(p => PriceCalculator.GetProductFinalPrice(p) >= min);
                filteredCombos = filteredCombos.Where(c => PriceCalculator.GetComboFinalPrice(c) >= min);
            }

            if (filter.MaxPrice.HasValue)
            {
                var max = (double)filter.MaxPrice.Value;
                filteredProducts = filteredProducts.Where(p => PriceCalculator.GetProductFinalPrice(p) <= max);
                filteredCombos = filteredCombos.Where(c => PriceCalculator.GetComboFinalPrice(c) <= max);
            }

            if (filter.OnlyDiscounted)
            {
                filteredProducts = filteredProducts.Where(p => p.HasDiscount);
                filteredCombos = filteredCombos.Where(c => c.HasAnyDiscount);
            }

            if (filter.Segment == "category" && filter.CategoryId.HasValue)
            {
                filteredProducts = filteredProducts.Where(p => p.CategoryId == filter.CategoryId.Value);
            }
            else if (filter.Segment == "combo")
            {
                filteredProducts = Enumerable.Empty<Product>();
            }

            var viewModel = new HomeViewModel
            {
                Products = filteredProducts
                    .OrderBy(p => p.Category?.Index ?? long.MaxValue)
                    .ThenBy(p => p.Id)
                    .ToList(),
                Combos = filteredCombos
                    .OrderBy(c => c.Index)
                    .ToList(),
                Categories = categories,
                Filter = filter
            };

            return View(viewModel);
        }

        public async Task<IActionResult> ProductDetail(long id, int? rating)
        {
            var product = await _context.Products
                .Include(p => p.ProductTypes)
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id && p.IsPublish);

            if (product == null)
                return NotFound();

            product.RefreshDerivedFields();

            var viewModel = new ProductDetailViewModel
            {
                Product = product,
                CanDeleteRating = User.HasClaim("DeleteEvaluate", "true")
            };

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                var completedItems = await _context.OrderItems
                    .Include(oi => oi.Order)
                    .Where(oi => !oi.IsDeleted && oi.ProductId == product.Id &&
                        oi.Order != null && !oi.Order.IsDeleted &&
                        oi.Order.UserId == userId && oi.Order.Status == OrderStatus.Completed)
                    .OrderByDescending(oi => oi.Order!.CreatedAt)
                    .ToListAsync();

                if (completedItems.Any())
                {
                    viewModel.CanRate = true;

                    var existingRating = await _context.Ratings
                        .AsNoTracking()
                        .Include(r => r.User)
                        .Where(r => !r.IsDeleted && r.UserId == userId && r.ProductId == product.Id)
                        .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (existingRating != null)
                    {
                        viewModel.OrderItemIdForRating = existingRating.OrderItemId;
                        viewModel.UserRating = new RatingDisplayViewModel
                        {
                            Id = existingRating.Id,
                            Score = existingRating.Score,
                            Comment = existingRating.Comment,
                            CreatedAt = existingRating.CreatedAt,
                            UpdatedAt = existingRating.UpdatedAt,
                            OrderItemId = existingRating.OrderItemId,
                            UserName = existingRating.User?.FullName
                                ?? existingRating.User?.UserName
                                ?? User.Identity?.Name
                                ?? "Bạn",
                            IsCurrentUser = true
                        };
                    }
                    else
                    {
                        viewModel.OrderItemIdForRating = completedItems.First().Id;
                    }
                }
            }

            int? normalizedRatingFilter = rating is >= 1 and <= 5 ? rating : null;

            var ratingsQuery = _context.Ratings
                .AsNoTracking()
                .Where(r => !r.IsDeleted && r.ProductId == product.Id);

            var ratingCountLookup = (await ratingsQuery
                .GroupBy(r => r.Score)
                .Select(g => new { Score = g.Key, Count = g.Count() })
                .ToListAsync())
                .ToDictionary(x => x.Score, x => x.Count);

            viewModel.RatingCounts = Enumerable.Range(1, 5)
                .ToDictionary(score => score, score => ratingCountLookup.TryGetValue(score, out var count) ? count : 0);

            viewModel.SelectedRatingFilter = normalizedRatingFilter;

            viewModel.Ratings = await ratingsQuery
                .Where(r => !normalizedRatingFilter.HasValue || r.Score == normalizedRatingFilter.Value)
                .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
                .Select(r => new RatingDisplayViewModel
                {
                    Id = r.Id,
                    Score = r.Score,
                    Comment = r.Comment,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt,
                    OrderItemId = r.OrderItemId,
                    UserName = r.User != null
                        ? (!string.IsNullOrWhiteSpace(r.User.FullName) ? r.User.FullName : r.User.UserName)
                        : "Khách hàng",
                    IsCurrentUser = r.UserId == userId
                })
                .ToListAsync();

            return View(viewModel);
        }

        public async Task<IActionResult> ComboDetail(long id, int? rating)
        {
            var combo = await _context.Combos
                .Include(c => c.ComboItems)
                    .ThenInclude(ci => ci.Product)
                        .ThenInclude(p => p.ProductTypes)
                .FirstOrDefaultAsync(c => c.Id == id && c.IsPublish);

            if (combo == null)
                return NotFound();

            combo.RefreshDerivedFields();

            var viewModel = new ComboDetailViewModel
            {
                Combo = combo,
                CanDeleteRating = User.HasClaim("DeleteEvaluate", "true")
            };

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                var completedItems = await _context.OrderItems
                    .Include(oi => oi.Order)
                    .Where(oi => !oi.IsDeleted && oi.ComboId == combo.Id &&
                        oi.Order != null && !oi.Order.IsDeleted &&
                        oi.Order.UserId == userId && oi.Order.Status == OrderStatus.Completed)
                    .OrderByDescending(oi => oi.Order!.CreatedAt)
                    .ToListAsync();

                if (completedItems.Any())
                {
                    viewModel.CanRate = true;

                    var existingRating = await _context.Ratings
                        .AsNoTracking()
                        .Include(r => r.User)
                        .Where(r => !r.IsDeleted && r.UserId == userId && r.ComboId == combo.Id)
                        .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (existingRating != null)
                    {
                        viewModel.OrderItemIdForRating = existingRating.OrderItemId;
                        viewModel.UserRating = new RatingDisplayViewModel
                        {
                            Id = existingRating.Id,
                            Score = existingRating.Score,
                            Comment = existingRating.Comment,
                            CreatedAt = existingRating.CreatedAt,
                            UpdatedAt = existingRating.UpdatedAt,
                            OrderItemId = existingRating.OrderItemId,
                            UserName = existingRating.User?.FullName
                                ?? existingRating.User?.UserName
                                ?? User.Identity?.Name
                                ?? "Bạn",
                            IsCurrentUser = true
                        };
                    }
                    else
                    {
                        viewModel.OrderItemIdForRating = completedItems.First().Id;
                    }
                }
            }

            int? normalizedComboRatingFilter = rating is >= 1 and <= 5 ? rating : null;

            var comboRatingsQuery = _context.Ratings
                .AsNoTracking()
                .Where(r => !r.IsDeleted && r.ComboId == combo.Id);

            var comboRatingCountsLookup = (await comboRatingsQuery
                .GroupBy(r => r.Score)
                .Select(g => new { Score = g.Key, Count = g.Count() })
                .ToListAsync())
                .ToDictionary(x => x.Score, x => x.Count);

            viewModel.RatingCounts = Enumerable.Range(1, 5)
                .ToDictionary(score => score, score => comboRatingCountsLookup.TryGetValue(score, out var count) ? count : 0);

            viewModel.SelectedRatingFilter = normalizedComboRatingFilter;

            viewModel.Ratings = await comboRatingsQuery
                .Where(r => !normalizedComboRatingFilter.HasValue || r.Score == normalizedComboRatingFilter.Value)
                .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
                .Select(r => new RatingDisplayViewModel
                {
                    Id = r.Id,
                    Score = r.Score,
                    Comment = r.Comment,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt,
                    OrderItemId = r.OrderItemId,
                    UserName = r.User != null
                        ? (!string.IsNullOrWhiteSpace(r.User.FullName) ? r.User.FullName : r.User.UserName)
                        : "Khách hàng",
                    IsCurrentUser = r.UserId == userId
                })
                .ToListAsync();

            return View(viewModel);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [Route("status-code/{code:int}")]
        public IActionResult StatusCodeHandler(int code)
        {
            if (code == StatusCodes.Status404NotFound)
            {
                return NotFoundPage();
            }

            Response.StatusCode = code;
            return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [Route("not-found")]
        [Route("404")]
        public IActionResult NotFoundPage()
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View("NotFound");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
