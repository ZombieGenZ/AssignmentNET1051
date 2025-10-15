using Assignment.Data;
using Assignment.Models;
using Assignment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Assignment.Controllers
{
    [Authorize]
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CartController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Cart
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var cart = await _context.LoadCartWithAvailableItemsAsync(userId);

            if (cart == null)
            {
                cart = new Cart
                {
                    UserId = userId,
                    CartItems = new List<CartItem>()
                };
            }

            return View(cart);
        }

        // POST: Cart/AddProduct
        [HttpPost]
        public async Task<IActionResult> AddProduct(long productId, long quantity = 1)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted && p.IsPublish);
            if (product == null)
            {
                return Json(new { success = false, error = "Sản phẩm không khả dụng." });
            }

            var cart = await _context.LoadCartWithAvailableItemsAsync(userId);

            if (cart == null)
            {
                cart = new Cart { UserId = userId, CartItems = new List<CartItem>() };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            var existingItem = cart.CartItems
                .FirstOrDefault(ci => ci.ProductId == productId);

            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                var cartItem = new CartItem
                {
                    CartId = cart.Id,
                    ProductId = productId,
                    Quantity = quantity
                };
                _context.CartItems.Add(cartItem);
            }

            await _context.SaveChangesAsync();

            var updatedCart = await _context.LoadCartWithAvailableItemsAsync(userId);

            // Tính toán và trả về số lượng mới
            var newCount = updatedCart?.CartItems.Sum(ci => ci.Quantity) ?? 0;
            return Json(new { success = true, count = newCount });
        }

        // POST: Cart/AddCombo
        [HttpPost]
        public async Task<IActionResult> AddCombo(long comboId, long quantity = 1)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var combo = await _context.Combos.FirstOrDefaultAsync(c => c.Id == comboId && !c.IsDeleted && c.IsPublish);
            if (combo == null)
            {
                return Json(new { success = false, error = "Combo không khả dụng." });
            }

            var cart = await _context.LoadCartWithAvailableItemsAsync(userId);

            if (cart == null)
            {
                cart = new Cart { UserId = userId, CartItems = new List<CartItem>() };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            var existingItem = cart.CartItems
                .FirstOrDefault(ci => ci.ComboId == comboId);

            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                var cartItem = new CartItem
                {
                    CartId = cart.Id,
                    ComboId = comboId,
                    Quantity = quantity
                };
                _context.CartItems.Add(cartItem);
            }

            await _context.SaveChangesAsync();

            var updatedCart = await _context.LoadCartWithAvailableItemsAsync(userId);

            // Tính toán và trả về số lượng mới
            var newCount = updatedCart?.CartItems.Sum(ci => ci.Quantity) ?? 0;
            return Json(new { success = true, count = newCount });
        }


        // POST: Cart/UpdateQuantity
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateQuantity(long cartItemId, long quantity)
        {
            if (quantity < 1)
            {
                return BadRequest("Số lượng phải lớn hơn 0");
            }

            var cartItem = await _context.CartItems.FindAsync(cartItemId);

            if (cartItem == null)
            {
                return NotFound();
            }

            cartItem.Quantity = quantity;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã cập nhật số lượng!";
            return RedirectToAction(nameof(Index));
        }

        // POST: Cart/Remove
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(long cartItemId)
        {
            var cartItem = await _context.CartItems.FindAsync(cartItemId);

            if (cartItem == null)
            {
                return NotFound();
            }

            _context.CartItems.Remove(cartItem);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xóa sản phẩm khỏi giỏ hàng!";
            return RedirectToAction(nameof(Index));
        }

        // POST: Cart/Clear
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Clear()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart != null && cart.CartItems.Any())
            {
                _context.CartItems.RemoveRange(cart.CartItems);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã xóa toàn bộ giỏ hàng!";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Cart/GetCount
        public async Task<IActionResult> GetCount()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var cart = await _context.LoadCartWithAvailableItemsAsync(userId);

            var count = cart?.CartItems.Sum(ci => ci.Quantity) ?? 0;

            return Json(new { count });
        }
    }
}
