using Assignment.Data;
using Assignment.Models;
using Assignment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Linq;

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ProceedToCheckout(long[] selectedItemIds)
        {
            if (selectedItemIds == null || selectedItemIds.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn ít nhất một sản phẩm để thanh toán.";
                return RedirectToAction(nameof(Index));
            }

            var normalizedIds = selectedItemIds
                .Where(id => id > 0)
                .Distinct()
                .ToArray();

            if (normalizedIds.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn ít nhất một sản phẩm để thanh toán.";
                return RedirectToAction(nameof(Index));
            }

            var idString = string.Join(',', normalizedIds);
            return RedirectToAction("Checkout", "Order", new { cartItemIds = idString });
        }

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

            var newCount = updatedCart?.CartItems.Sum(ci => ci.Quantity) ?? 0;
            return Json(new { success = true, count = newCount });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BuyNowProduct(long productId, long quantity = 1)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted && p.IsPublish);
            if (product == null)
            {
                return Json(new { success = false, error = "Sản phẩm không khả dụng." });
            }

            var normalizedQuantity = quantity < 1 ? 1 : quantity;
            if (product.Stock > 0 && normalizedQuantity > product.Stock)
            {
                normalizedQuantity = product.Stock;
            }

            var cart = await _context.LoadCartWithAvailableItemsAsync(userId);

            if (cart == null)
            {
                cart = new Cart { UserId = userId, CartItems = new List<CartItem>() };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            var existingItem = cart.CartItems.FirstOrDefault(ci => ci.ProductId == productId);
            if (existingItem != null)
            {
                existingItem.Quantity = normalizedQuantity;
            }
            else
            {
                existingItem = new CartItem
                {
                    CartId = cart.Id,
                    ProductId = productId,
                    Quantity = normalizedQuantity
                };
                _context.CartItems.Add(existingItem);
            }

            await _context.SaveChangesAsync();

            var redirectUrl = Url.Action("Checkout", "Order", new { cartItemIds = existingItem.Id });
            return Json(new { success = true, redirectUrl });
        }

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

            var newCount = updatedCart?.CartItems.Sum(ci => ci.Quantity) ?? 0;
            return Json(new { success = true, count = newCount });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BuyNowCombo(long comboId, long quantity = 1)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var combo = await _context.Combos.FirstOrDefaultAsync(c => c.Id == comboId && !c.IsDeleted && c.IsPublish);
            if (combo == null)
            {
                return Json(new { success = false, error = "Combo không khả dụng." });
            }

            var normalizedQuantity = quantity < 1 ? 1 : quantity;
            if (combo.Stock > 0 && normalizedQuantity > combo.Stock)
            {
                normalizedQuantity = combo.Stock;
            }

            var cart = await _context.LoadCartWithAvailableItemsAsync(userId);

            if (cart == null)
            {
                cart = new Cart { UserId = userId, CartItems = new List<CartItem>() };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            var existingItem = cart.CartItems.FirstOrDefault(ci => ci.ComboId == comboId);
            if (existingItem != null)
            {
                existingItem.Quantity = normalizedQuantity;
            }
            else
            {
                existingItem = new CartItem
                {
                    CartId = cart.Id,
                    ComboId = comboId,
                    Quantity = normalizedQuantity
                };
                _context.CartItems.Add(existingItem);
            }

            await _context.SaveChangesAsync();

            var redirectUrl = Url.Action("Checkout", "Order", new { cartItemIds = existingItem.Id });
            return Json(new { success = true, redirectUrl });
        }


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

        public async Task<IActionResult> GetCount()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var cart = await _context.LoadCartWithAvailableItemsAsync(userId);

            var count = cart?.CartItems.Sum(ci => ci.Quantity) ?? 0;

            return Json(new { count });
        }
    }
}
