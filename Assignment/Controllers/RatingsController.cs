using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Assignment.Data;
using Assignment.Enums;
using Assignment.Models;
using Assignment.ViewModels.Ratings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Assignment.Controllers
{
    [Authorize]
    public class RatingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public RatingsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            }

            var completedItems = await _context.OrderItems
                .AsNoTracking()
                .Include(oi => oi.Order)
                .Include(oi => oi.Product)
                .Include(oi => oi.Combo)
                .Where(oi => !oi.IsDeleted && oi.Order != null && !oi.Order.IsDeleted &&
                    oi.Order.UserId == userId && oi.Order.Status == OrderStatus.Completed)
                .OrderByDescending(oi => oi.Order!.CreatedAt)
                .ToListAsync();

            var orderItemIds = completedItems.Select(oi => oi.Id).ToList();

            var ratings = await _context.Ratings
                .AsNoTracking()
                .Where(r => !r.IsDeleted && r.UserId == userId && orderItemIds.Contains(r.OrderItemId))
                .ToListAsync();

            var ratingLookup = ratings.ToDictionary(r => r.OrderItemId);
            var items = new List<RatingItemViewModel>();

            foreach (var item in completedItems)
            {
                ratingLookup.TryGetValue(item.Id, out var existingRating);

                var isProductAvailable = item.Product != null && !item.Product.IsDeleted && item.Product.IsPublish;
                var isComboAvailable = item.Combo != null && !item.Combo.IsDeleted && item.Combo.IsPublish;
                var itemName = item.Product?.Name ?? item.Combo?.Name ?? "Sản phẩm";
                var itemType = item.Product != null ? "Sản phẩm" : item.Combo != null ? "Combo" : "Khác";
                var imageUrl = item.Product?.ProductImageUrl ?? item.Combo?.ImageUrl;

                items.Add(new RatingItemViewModel
                {
                    OrderItemId = item.Id,
                    OrderId = item.OrderId,
                    OrderCreatedAt = item.Order?.CreatedAt ?? item.CreatedAt,
                    ItemName = itemName,
                    ItemType = itemType,
                    ImageUrl = imageUrl,
                    Quantity = item.Quantity,
                    Price = item.Price,
                    IsAvailable = isProductAvailable || isComboAvailable,
                    Score = existingRating?.Score,
                    Comment = existingRating?.Comment,
                    CanRate = true,
                    ProductId = item.ProductId,
                    ComboId = item.ComboId
                });
            }

            var viewModel = new RatingIndexViewModel
            {
                Items = items
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(RatingInputModel input)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Vui lòng kiểm tra thông tin đánh giá.";
                return RedirectToAction(nameof(Index));
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            }

            var orderItem = await _context.OrderItems
                .Include(oi => oi.Order)
                .Include(oi => oi.Product)
                .Include(oi => oi.Combo)
                .FirstOrDefaultAsync(oi => oi.Id == input.OrderItemId && !oi.IsDeleted);

            if (orderItem == null || orderItem.Order == null || orderItem.Order.IsDeleted || orderItem.Order.UserId != userId)
            {
                TempData["Error"] = "Bạn không thể đánh giá mục này.";
                return RedirectToAction(nameof(Index));
            }

            if (orderItem.Order.Status != OrderStatus.Completed)
            {
                TempData["Error"] = "Chỉ có thể đánh giá sản phẩm thuộc đơn hàng đã hoàn tất.";
                return RedirectToAction(nameof(Index));
            }

            var rating = await _context.Ratings
                .FirstOrDefaultAsync(r => r.OrderItemId == orderItem.Id && r.UserId == userId);

            if (rating == null)
            {
                rating = new Rating
                {
                    OrderItemId = orderItem.Id,
                    UserId = userId,
                    Score = input.Score,
                    Comment = input.Comment,
                    ProductId = orderItem.ProductId,
                    ComboId = orderItem.ComboId,
                    CreateBy = userId,
                    CreatedAt = DateTime.Now,
                    IsDeleted = false
                };

                _context.Ratings.Add(rating);
            }
            else
            {
                if (rating.IsDeleted)
                {
                    rating.IsDeleted = false;
                    rating.DeletedAt = null;
                }

                rating.Score = input.Score;
                rating.Comment = input.Comment;
                rating.ProductId = orderItem.ProductId;
                rating.ComboId = orderItem.ComboId;
                rating.UpdatedAt = DateTime.Now;

                _context.Ratings.Update(rating);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Cảm ơn bạn đã gửi đánh giá!";

            return RedirectToAction(nameof(Index));
        }
    }
}
