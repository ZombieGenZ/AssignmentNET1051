using Assignment.Data;
using Assignment.Enums;
using Assignment.Models;
using Assignment.Services;
using Assignment.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Diagnostics;
using System.Linq;

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
                .Where(p => p.IsPublish && !p.IsDeleted)
                .Include(p => p.Category)
                .OrderBy(p => p.Category.Index)
                .ThenBy(p => p.Id)
                .ToListAsync();

            var combos = await _context.Combos
                .Include(c => c.ComboItems)
                    .ThenInclude(ci => ci.Product)
                .Where(c => !c.IsDeleted && c.IsPublish)
                .OrderBy(c => c.Index)
                .ToListAsync();

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
                filteredProducts = filteredProducts.Where(p => p.DiscountType != DiscountType.None);
                filteredCombos = filteredCombos.Where(c => c.DiscountType != DiscountType.None);
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

        public async Task<IActionResult> ProductDetail(long id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id && p.IsPublish);

            if (product == null)
                return NotFound();

            return View(product);
        }

        public async Task<IActionResult> ComboDetail(long id)
        {
            var combo = await _context.Combos
                .Include(c => c.ComboItems)
                    .ThenInclude(ci => ci.Product)
                .FirstOrDefaultAsync(c => c.Id == id && c.IsPublish);

            if (combo == null)
                return NotFound();

            return View(combo);
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
