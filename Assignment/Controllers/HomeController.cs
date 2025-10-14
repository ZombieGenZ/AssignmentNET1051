using Assignment.Data;
using Assignment.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

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

        public async Task<IActionResult> Index()
        {
            var products = await _context.Products
                .Where(p => p.IsPublish)
                .Include(p => p.Category)
                .Where(p => !p.IsDeleted && p.IsPublish)
                .OrderBy(p => p.Category.Index)
                .ThenBy(p => p.Id)
                .ToListAsync();

            var combos = await _context.Combos
                .Include(c => c.ComboItems)
                    .ThenInclude(ci => ci.Product)
                    .Where(c => !c.IsDeleted && c.IsPublish)
                .Where(c => c.IsPublish)
                .OrderBy(c => c.Index)
                .ToListAsync();

            var viewModel = new HomeViewModel
            {
                Products = products,
                Combos = combos
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

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
