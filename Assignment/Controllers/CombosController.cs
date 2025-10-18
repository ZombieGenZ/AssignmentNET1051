using Assignment.Data;
using Assignment.Models;
using Assignment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using Assignment.Extensions;

namespace Assignment.Controllers
{
    [Authorize] // Thêm Authorize để đảm bảo người dùng đã đăng nhập
    public class CombosController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthorizationService _authorizationService;

        public CombosController(ApplicationDbContext context, IAuthorizationService authorizationService)
        {
            _context = context;
            _authorizationService = authorizationService;
        }

        // GET: Combos
        // Hiển thị danh sách các combo chưa bị xóa mềm.
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var hasGetAll = User.HasPermission("GetComboAll");

            // Bắt đầu câu truy vấn bằng cách lọc ra các bản ghi đã bị xóa mềm.
            IQueryable<Combo> combos = _context.Combos
                .Where(c => !c.IsDeleted)
                .Include(c => c.ComboItems)
                .ThenInclude(ci => ci.Product);

            if (!hasGetAll)
            {
                if (User.HasPermission("GetCombo"))
                {
                    combos = combos.Where(c => c.CreateBy == userId);
                }
                else
                {
                    return Forbid();
                }
            }

            return View(await combos.ToListAsync());
        }

        // GET: Combos/Details/5
        // Chỉ hiển thị chi tiết nếu combo chưa bị xóa.
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Tìm combo nếu nó chưa bị xóa mềm.
            var combo = await _context.Combos
                .Where(c => !c.IsDeleted) // Lọc combo chưa bị xóa
                .Include(c => c.ComboItems)
                .ThenInclude(ci => ci.Product)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (combo == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, combo, "GetComboPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            return View(combo);
        }

        // GET: Combos/Create
        [Authorize(Policy = "CreateComboPolicy")]
        public async Task<IActionResult> Create()
        {
            var products = await GetAuthorizedProducts();
            ViewData["Products"] = new SelectList(products, "Id", "Name");
            return View();
        }

        // POST: Combos/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CreateComboPolicy")]
        public async Task<IActionResult> Create([Bind("Name,Description,Stock,DiscountType,Discount,IsPublish,ImageUrl,Index")] Combo combo, List<long> ProductIds, List<long> Quantities)
        {
            var selectedProductIds = ProductIds ?? new List<long>();
            var selectedQuantities = Quantities ?? new List<long>();
            var comboItemsInput = new List<(long productId, long quantity)>();

            if (selectedProductIds.Count != selectedQuantities.Count)
            {
                ModelState.AddModelError(string.Empty, "Danh sách sản phẩm không hợp lệ.");
            }
            else
            {
                for (int i = 0; i < selectedProductIds.Count; i++)
                {
                    if (selectedProductIds[i] <= 0 || selectedQuantities[i] <= 0)
                    {
                        ModelState.AddModelError(string.Empty, "Sản phẩm và số lượng phải lớn hơn 0.");
                        break;
                    }

                    comboItemsInput.Add((selectedProductIds[i], selectedQuantities[i]));
                }

                if (!comboItemsInput.Any())
                {
                    ModelState.AddModelError(string.Empty, "Combo phải có ít nhất một sản phẩm.");
                }
            }

            if (combo.DiscountType == Enums.DiscountType.None)
            {
                combo.Discount = null;
            }
            else if (combo.DiscountType == Enums.DiscountType.FixedAmount)
            {
                ModelState.AddModelError(nameof(combo.DiscountType), "Combo không hỗ trợ giảm giá cố định.");
            }

            if (ModelState.IsValid)
            {
                var distinctProductIds = comboItemsInput.Select(ci => ci.productId).Distinct().ToList();
                var products = await _context.Products
                    .Where(p => distinctProductIds.Contains(p.Id) && !p.IsDeleted)
                    .ToListAsync();

                if (products.Count != distinctProductIds.Count)
                {
                    ModelState.AddModelError(string.Empty, "Có sản phẩm không hợp lệ hoặc đã bị xóa.");
                }
                else
                {
                    var productLookup = products.ToDictionary(p => p.Id);
                    var priceItems = comboItemsInput
                        .Select(item => productLookup.TryGetValue(item.productId, out var product)
                            ? (product, item.quantity)
                            : ((Product?)null, item.quantity));

                    combo.Price = PriceCalculator.GetComboBasePrice(priceItems);
                    combo.CreateBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    combo.CreatedAt = DateTime.Now;
                    combo.UpdatedAt = null;
                    combo.DeletedAt = null;
                    combo.IsDeleted = false;

                    _context.Add(combo);
                    await _context.SaveChangesAsync();

                    foreach (var item in comboItemsInput)
                    {
                        var comboItem = new ComboItem
                        {
                            ComboId = combo.Id,
                            ProductId = item.productId,
                            Quantity = item.quantity,
                            CreateBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                            CreatedAt = DateTime.Now,
                            IsDeleted = false
                        };
                        _context.ComboItems.Add(comboItem);
                    }

                    await _context.SaveChangesAsync();

                    return RedirectToAction(nameof(Index));
                }
            }

            // Giữ lại dữ liệu sản phẩm khi có lỗi
            var authorizedProducts = await GetAuthorizedProducts();
            ViewData["Products"] = new SelectList(authorizedProducts, "Id", "Name");
            ViewData["SelectedProductIds"] = selectedProductIds;
            ViewData["SelectedQuantities"] = selectedQuantities;

            return View(combo);
        }

        // GET: Combos/Edit/5
        // Chỉ cho phép chỉnh sửa nếu combo chưa bị xóa.
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Tìm combo nếu nó chưa bị xóa mềm.
            var combo = await _context.Combos
                .Where(c => !c.IsDeleted) // Lọc combo chưa bị xóa
                .Include(c => c.ComboItems)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (combo == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, combo, "UpdateComboPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            var products = await GetAuthorizedProducts();
            ViewData["Products"] = new SelectList(products, "Id", "Name");
            return View(combo);
        }

        // POST: Combos/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("Name,Description,Stock,DiscountType,Discount,IsPublish,ImageUrl,Id,Index")] Combo combo, List<long> ProductIds, List<long> Quantities)
        {
            if (id != combo.Id)
            {
                return NotFound();
            }

            // Đảm bảo combo đang được chỉnh sửa tồn tại và chưa bị xóa mềm.
            var existingCombo = await _context.Combos
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

            if (existingCombo == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, existingCombo, "UpdateComboPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            var selectedProductIds = ProductIds ?? new List<long>();
            var selectedQuantities = Quantities ?? new List<long>();
            var comboItemsInput = new List<(long productId, long quantity)>();

            if (selectedProductIds.Count != selectedQuantities.Count)
            {
                ModelState.AddModelError(string.Empty, "Danh sách sản phẩm không hợp lệ.");
            }
            else
            {
                for (int i = 0; i < selectedProductIds.Count; i++)
                {
                    if (selectedProductIds[i] <= 0 || selectedQuantities[i] <= 0)
                    {
                        ModelState.AddModelError(string.Empty, "Sản phẩm và số lượng phải lớn hơn 0.");
                        break;
                    }

                    comboItemsInput.Add((selectedProductIds[i], selectedQuantities[i]));
                }

                if (!comboItemsInput.Any())
                {
                    ModelState.AddModelError(string.Empty, "Combo phải có ít nhất một sản phẩm.");
                }
            }

            if (combo.DiscountType == Enums.DiscountType.None)
            {
                combo.Discount = null;
            }
            else if (combo.DiscountType == Enums.DiscountType.FixedAmount)
            {
                ModelState.AddModelError(nameof(combo.DiscountType), "Combo không hỗ trợ giảm giá cố định.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var distinctProductIds = comboItemsInput.Select(ci => ci.productId).Distinct().ToList();
                    var products = await _context.Products
                        .Where(p => distinctProductIds.Contains(p.Id) && !p.IsDeleted)
                        .ToListAsync();

                    if (products.Count != distinctProductIds.Count)
                    {
                        ModelState.AddModelError(string.Empty, "Có sản phẩm không hợp lệ hoặc đã bị xóa.");
                    }
                    else
                    {
                        var productLookup = products.ToDictionary(p => p.Id);
                        var priceItems = comboItemsInput
                            .Select(item => productLookup.TryGetValue(item.productId, out var product)
                                ? (product, item.quantity)
                                : ((Product?)null, item.quantity));

                        combo.Price = PriceCalculator.GetComboBasePrice(priceItems);
                        combo.CreateBy = existingCombo.CreateBy;
                        combo.CreatedAt = existingCombo.CreatedAt;
                        combo.IsDeleted = existingCombo.IsDeleted;
                        combo.DeletedAt = existingCombo.DeletedAt;
                        combo.UpdatedAt = DateTime.Now;

                        _context.Update(combo);

                        var existingItems = _context.ComboItems.Where(ci => ci.ComboId == id);
                        _context.ComboItems.RemoveRange(existingItems);

                        foreach (var item in comboItemsInput)
                        {
                            var comboItem = new ComboItem
                            {
                                ComboId = combo.Id,
                                ProductId = item.productId,
                                Quantity = item.quantity,
                                CreateBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                                CreatedAt = DateTime.Now,
                                IsDeleted = false
                            };
                            _context.ComboItems.Add(comboItem);
                        }

                        await _context.SaveChangesAsync();

                        return RedirectToAction(nameof(Index));
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ComboExists(combo.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            var authorizedProducts = await GetAuthorizedProducts();
            ViewData["Products"] = new SelectList(authorizedProducts, "Id", "Name");
            ViewData["SelectedProductIds"] = selectedProductIds;
            ViewData["SelectedQuantities"] = selectedQuantities;

            return View(combo);
        }

        // GET: Combos/Delete/5
        // Hiển thị trang xác nhận xóa nếu combo chưa bị xóa.
        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Tìm combo nếu nó chưa bị xóa mềm.
            var combo = await _context.Combos
                .Where(c => !c.IsDeleted) // Lọc combo chưa bị xóa
                .Include(c => c.ComboItems)
                .ThenInclude(ci => ci.Product)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (combo == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, combo, "DeleteComboPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            return View(combo);
        }

        // POST: Combos/Delete/5
        // Thực hiện xóa mềm.
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            // Tải combo và các item liên quan.
            // Chỉ tìm combo chưa bị xóa mềm.
            var combo = await _context.Combos
                                  .Include(c => c.ComboItems)
                                  .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

            if (combo == null)
            {
                // Combo có thể đã bị người khác xóa. Chuyển hướng về trang danh sách là an toàn.
                return RedirectToAction(nameof(Index));
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, combo, "DeleteComboPolicy");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            // Thực hiện xóa mềm bằng cách đặt cờ và dấu thời gian.
            combo.IsDeleted = true;
            combo.DeletedAt = DateTime.Now;

            // Xóa mềm các ComboItems liên quan
            if (combo.ComboItems != null)
            {
                foreach (var item in combo.ComboItems)
                {
                    item.IsDeleted = true;
                    // Bạn có thể muốn thêm cả DeletedAt cho ComboItem nếu model có.
                }
            }

            _context.Combos.Update(combo);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ComboExists(long id)
        {
            // Phương thức này cũng cần phải bỏ qua các combo đã bị xóa mềm.
            return _context.Combos.Any(e => e.Id == id && !e.IsDeleted);
        }

        [NonAction]
        private async Task<List<Product>> GetAuthorizedProducts()
        {
            var hasGetProductAll = User.HasPermission("GetProductAll");
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // Chỉ lấy các sản phẩm chưa bị xóa và được publish
            IQueryable<Product> query = _context.Products.Where(p => !p.IsDeleted && p.IsPublish);

            if (hasGetProductAll)
            {
                return await query.ToListAsync();
            }
            else
            {
                return await query.Where(c => c.CreateBy == userId).ToListAsync();
            }
        }
    }
}
