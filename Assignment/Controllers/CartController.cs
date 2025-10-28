using Assignment.Data;
using Assignment.Models;
using Assignment.Services;
using Assignment.ViewModels.Cart;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Linq;
using System.Collections.Generic;
using System;

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
        public IActionResult ProceedToCheckout(long[] selectedItemIds, long[]? selectedSelectionIds)
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

            var normalizedSelectionIds = selectedSelectionIds?
                .Where(id => id > 0)
                .Distinct()
                .ToArray() ?? Array.Empty<long>();

            var idString = string.Join(',', normalizedIds);
            var selectionIdString = normalizedSelectionIds.Length > 0
                ? string.Join(',', normalizedSelectionIds)
                : null;

            var routeValues = new Microsoft.AspNetCore.Routing.RouteValueDictionary
            {
                ["cartItemIds"] = idString
            };

            if (!string.IsNullOrEmpty(selectionIdString))
            {
                routeValues["selectedSelectionIds"] = selectionIdString;
            }

            return RedirectToAction("Checkout", "Order", routeValues);
        }

        [HttpPost]
        public async Task<IActionResult> AddProduct([FromBody] AddProductToCartRequest request)
        {
            if (request == null)
            {
                return Json(new { success = false, error = "Dữ liệu không hợp lệ." });
            }

            if (!ModelState.IsValid)
            {
                return Json(new { success = false, error = "Vui lòng chọn loại sản phẩm hợp lệ." });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var product = await _context.Products
                .Include(p => p.ProductTypes)
                .FirstOrDefaultAsync(p => p.Id == request.ProductId && !p.IsDeleted);
            if (product == null)
            {
                return Json(new { success = false, error = "Sản phẩm không khả dụng." });
            }

            product.RefreshDerivedFields();

            if (!product.IsPublish)
            {
                return Json(new { success = false, error = "Sản phẩm không khả dụng." });
            }

            var normalizedSelections = NormalizeSelections(product, request.Selections);
            if (!normalizedSelections.Any())
            {
                return Json(new { success = false, error = "Vui lòng chọn ít nhất một loại sản phẩm khả dụng." });
            }

            var normalizedExtras = await NormalizeExtrasAsync(request.ProductId, request.Extras);

            var cart = await _context.LoadCartWithAvailableItemsAsync(userId);

            if (cart == null)
            {
                cart = new Cart { UserId = userId, CartItems = new List<CartItem>() };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            var existingItem = cart.CartItems
                .FirstOrDefault(ci => ci.ProductId == request.ProductId);

            if (existingItem == null)
            {
                existingItem = new CartItem
                {
                    CartId = cart.Id,
                    ProductId = request.ProductId,
                    Quantity = 0
                };
                _context.CartItems.Add(existingItem);
            }

            await _context.Entry(existingItem)
                .Collection(ci => ci.ProductTypeSelections)
                .LoadAsync();
            await _context.Entry(existingItem)
                .Collection(ci => ci.ProductExtraSelections)
                .LoadAsync();

            foreach (var selection in normalizedSelections)
            {
                var existingSelection = existingItem.ProductTypeSelections
                    .FirstOrDefault(s => s.ProductTypeId == selection.ProductType.Id);

                if (existingSelection != null)
                {
                    existingSelection.Quantity += selection.Quantity;
                    existingSelection.UnitPrice = selection.UnitPrice;
                }
                else
                {
                    existingItem.ProductTypeSelections.Add(new CartItemProductType
                    {
                        ProductTypeId = selection.ProductType.Id,
                        Quantity = selection.Quantity,
                        UnitPrice = selection.UnitPrice
                    });
                }
            }

            foreach (var extra in normalizedExtras)
            {
                var existingExtra = existingItem.ProductExtraSelections
                    .FirstOrDefault(e => e.ProductExtraId == extra.Extra.Id);

                if (existingExtra != null)
                {
                    existingExtra.Quantity += extra.Quantity;
                    existingExtra.UnitPrice = extra.UnitPrice;
                }
                else
                {
                    existingItem.ProductExtraSelections.Add(new CartItemProductExtra
                    {
                        ProductExtraId = extra.Extra.Id,
                        Quantity = extra.Quantity,
                        UnitPrice = extra.UnitPrice
                    });
                }
            }

            existingItem.Quantity = existingItem.ProductTypeSelections.Sum(s => (long)s.Quantity);

            await _context.SaveChangesAsync();

            var updatedCart = await _context.LoadCartWithAvailableItemsAsync(userId);

            var newCount = updatedCart?.CartItems.Sum(ci => ci.Quantity) ?? 0;
            return Json(new { success = true, count = newCount });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BuyNowProduct([FromBody] AddProductToCartRequest request)
        {
            if (request == null)
            {
                return Json(new { success = false, error = "Dữ liệu không hợp lệ." });
            }

            if (!ModelState.IsValid)
            {
                return Json(new { success = false, error = "Vui lòng chọn loại sản phẩm hợp lệ." });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var product = await _context.Products
                .Include(p => p.ProductTypes)
                .FirstOrDefaultAsync(p => p.Id == request.ProductId && !p.IsDeleted);
            if (product == null)
            {
                return Json(new { success = false, error = "Sản phẩm không khả dụng." });
            }

            product.RefreshDerivedFields();

            if (!product.IsPublish)
            {
                return Json(new { success = false, error = "Sản phẩm không khả dụng." });
            }

            var normalizedSelections = NormalizeSelections(product, request.Selections);
            if (!normalizedSelections.Any())
            {
                return Json(new { success = false, error = "Vui lòng chọn ít nhất một loại sản phẩm khả dụng." });
            }

            var normalizedExtras = await NormalizeExtrasAsync(request.ProductId, request.Extras);

            var cart = await _context.LoadCartWithAvailableItemsAsync(userId);

            if (cart == null)
            {
                cart = new Cart { UserId = userId, CartItems = new List<CartItem>() };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            var existingItem = cart.CartItems.FirstOrDefault(ci => ci.ProductId == request.ProductId);
            if (existingItem == null)
            {
                existingItem = new CartItem
                {
                    CartId = cart.Id,
                    ProductId = request.ProductId
                };
                _context.CartItems.Add(existingItem);
            }

            await _context.Entry(existingItem)
                .Collection(ci => ci.ProductTypeSelections)
                .LoadAsync();
            await _context.Entry(existingItem)
                .Collection(ci => ci.ProductExtraSelections)
                .LoadAsync();

            if (existingItem.ProductTypeSelections.Any())
            {
                _context.CartItemProductTypes.RemoveRange(existingItem.ProductTypeSelections);
                existingItem.ProductTypeSelections.Clear();
            }

            if (existingItem.ProductExtraSelections.Any())
            {
                _context.CartItemProductExtras.RemoveRange(existingItem.ProductExtraSelections);
                existingItem.ProductExtraSelections.Clear();
            }

            foreach (var selection in normalizedSelections)
            {
                existingItem.ProductTypeSelections.Add(new CartItemProductType
                {
                    ProductTypeId = selection.ProductType.Id,
                    Quantity = selection.Quantity,
                    UnitPrice = selection.UnitPrice
                });
            }

            foreach (var extra in normalizedExtras)
            {
                existingItem.ProductExtraSelections.Add(new CartItemProductExtra
                {
                    ProductExtraId = extra.Extra.Id,
                    Quantity = extra.Quantity,
                    UnitPrice = extra.UnitPrice
                });
            }

            existingItem.Quantity = existingItem.ProductTypeSelections.Sum(s => (long)s.Quantity);

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

            var cartItem = await _context.CartItems
                .Include(ci => ci.ProductTypeSelections)
                .FirstOrDefaultAsync(ci => ci.Id == cartItemId);

            if (cartItem == null)
            {
                return NotFound();
            }

            if (cartItem.ProductId.HasValue)
            {
                return BadRequest("Vui lòng cập nhật số lượng theo từng loại sản phẩm.");
            }

            cartItem.Quantity = quantity;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã cập nhật số lượng!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProductTypeQuantity(long selectionId, int quantity)
        {
            if (quantity < 1)
            {
                return BadRequest("Số lượng phải lớn hơn 0");
            }

            var selection = await _context.CartItemProductTypes
                .Include(s => s.CartItem)
                .ThenInclude(ci => ci.ProductTypeSelections)
                .FirstOrDefaultAsync(s => s.Id == selectionId);

            if (selection == null || selection.CartItem == null)
            {
                return NotFound();
            }

            selection.Quantity = quantity;
            selection.CartItem.Quantity = selection.CartItem.ProductTypeSelections.Sum(s => (long)s.Quantity);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã cập nhật số lượng loại sản phẩm!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveProductType(long selectionId)
        {
            var selection = await _context.CartItemProductTypes
                .Include(s => s.CartItem)
                .ThenInclude(ci => ci.ProductTypeSelections)
                .FirstOrDefaultAsync(s => s.Id == selectionId);

            if (selection == null || selection.CartItem == null)
            {
                return NotFound();
            }

            _context.CartItemProductTypes.Remove(selection);

            selection.CartItem.ProductTypeSelections.Remove(selection);
            selection.CartItem.Quantity = selection.CartItem.ProductTypeSelections.Sum(s => (long)s.Quantity);

            if (selection.CartItem.Quantity <= 0)
            {
                _context.CartItems.Remove(selection.CartItem);
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xóa loại sản phẩm khỏi giỏ hàng!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProductExtraQuantity(long selectionId, int quantity)
        {
            if (quantity < 1)
            {
                return BadRequest("Số lượng phải lớn hơn 0");
            }

            var selection = await _context.CartItemProductExtras
                .Include(s => s.CartItem)
                .FirstOrDefaultAsync(s => s.Id == selectionId);

            if (selection == null)
            {
                return NotFound();
            }

            selection.Quantity = quantity;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã cập nhật số lượng sản phẩm bổ sung!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveProductExtra(long selectionId)
        {
            var selection = await _context.CartItemProductExtras
                .Include(s => s.CartItem)
                .FirstOrDefaultAsync(s => s.Id == selectionId);

            if (selection == null)
            {
                return NotFound();
            }

            _context.CartItemProductExtras.Remove(selection);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xóa sản phẩm bổ sung khỏi giỏ hàng!";
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

        private static List<(ProductType ProductType, int Quantity, double UnitPrice)> NormalizeSelections(Product product, IEnumerable<ProductTypeSelectionRequest> selections)
        {
            var results = new List<(ProductType ProductType, int Quantity, double UnitPrice)>();
            if (product == null || selections == null)
            {
                return results;
            }

            var activeTypes = product.ProductTypes?
                .Where(pt => !pt.IsDeleted && pt.IsPublish)
                .ToDictionary(pt => pt.Id)
                ?? new Dictionary<long, ProductType>();

            if (!activeTypes.Any())
            {
                return results;
            }

            foreach (var grouping in selections
                .Where(selection => selection != null)
                .GroupBy(selection => selection.ProductTypeId))
            {
                if (!activeTypes.TryGetValue(grouping.Key, out var productType))
                {
                    continue;
                }

                var totalQuantity = grouping.Sum(selection => Math.Max(selection.Quantity, 0));
                if (totalQuantity <= 0)
                {
                    continue;
                }

                if (productType.Stock > 0)
                {
                    totalQuantity = Math.Min(totalQuantity, productType.Stock);
                }

                if (totalQuantity <= 0)
                {
                    continue;
                }

                var unitPrice = PriceCalculator.GetProductTypeFinalPrice(productType);
                results.Add((productType, totalQuantity, unitPrice));
            }

            return results;
        }

        private async Task<List<(ProductExtra Extra, int Quantity, double UnitPrice)>> NormalizeExtrasAsync(long productId, IEnumerable<ProductExtraSelectionRequest>? extras)
        {
            var results = new List<(ProductExtra Extra, int Quantity, double UnitPrice)>();
            if (extras == null)
            {
                return results;
            }

            var groupedExtras = extras
                .Where(extra => extra != null)
                .GroupBy(extra => extra.ProductExtraId)
                .ToList();

            if (!groupedExtras.Any())
            {
                return results;
            }

            var extraIds = groupedExtras.Select(group => group.Key).ToList();

            var applicableExtras = await _context.ProductExtras
                .Include(extra => extra.ProductExtraProducts)
                .Where(extra => !extra.IsDeleted && extra.IsPublish && extraIds.Contains(extra.Id))
                .ToListAsync();

            var applicableMap = applicableExtras
                .Where(extra => extra.ProductExtraProducts.Any(link => !link.IsDeleted && link.ProductId == productId))
                .ToDictionary(extra => extra.Id);

            foreach (var grouping in groupedExtras)
            {
                if (!applicableMap.TryGetValue(grouping.Key, out var extra))
                {
                    continue;
                }

                var totalQuantity = grouping.Sum(item => Math.Max(item.Quantity, 0));
                if (totalQuantity <= 0)
                {
                    continue;
                }

                if (extra.Stock > 0)
                {
                    totalQuantity = Math.Min(totalQuantity, extra.Stock);
                }

                if (totalQuantity <= 0)
                {
                    continue;
                }

                var unitPrice = PriceCalculator.GetProductExtraFinalPrice(extra);
                results.Add((extra, totalQuantity, unitPrice));
            }

            return results;
        }
    }
}
