using System;
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(RatingInputModel input)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Vui lòng kiểm tra thông tin đánh giá.";
                return RedirectToLocal(input.ReturnUrl) ?? RedirectToAction("Index", "Home");
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
                return RedirectToLocal(input.ReturnUrl) ?? RedirectToAction("Index", "Home");
            }

            if (orderItem.Order.Status != OrderStatus.Completed)
            {
                TempData["Error"] = "Chỉ có thể đánh giá sản phẩm thuộc đơn hàng đã hoàn tất.";
                return RedirectToLocal(input.ReturnUrl) ?? RedirectToAction("Index", "Home");
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

            await UpdateAggregateAsync(rating.ProductId, rating.ComboId);

            await _context.SaveChangesAsync();
            TempData["Success"] = "Cảm ơn bạn đã gửi đánh giá!";

            return RedirectToLocal(input.ReturnUrl) ?? RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(long id, string? returnUrl)
        {
            if (!User.HasClaim("DeleteEvaluate", "true"))
            {
                return Forbid();
            }

            var rating = await _context.Ratings
                .FirstOrDefaultAsync(r => r.Id == id);

            if (rating == null)
            {
                TempData["Error"] = "Không tìm thấy đánh giá.";
                return RedirectToLocal(returnUrl) ?? RedirectToAction("Index", "Home");
            }

            if (!rating.IsDeleted)
            {
                rating.IsDeleted = true;
                rating.DeletedAt = DateTime.Now;
                rating.UpdatedAt = DateTime.Now;

                _context.Ratings.Update(rating);

                await _context.SaveChangesAsync();

                await UpdateAggregateAsync(rating.ProductId, rating.ComboId);

                await _context.SaveChangesAsync();

                TempData["Success"] = "Đánh giá đã được xóa.";
            }

            return RedirectToLocal(returnUrl) ?? RedirectToAction("Index", "Home");
        }

        private IActionResult? RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return null;
        }

        private async Task UpdateAggregateAsync(long? productId, long? comboId)
        {
            if (productId.HasValue)
            {
                var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId.Value);
                if (product != null)
                {
                    var scores = await _context.Ratings
                        .Where(r => !r.IsDeleted && r.ProductId == product.Id)
                        .Select(r => r.Score)
                        .ToListAsync();

                    product.TotalEvaluate = scores.Count;
                    product.AverageEvaluate = scores.Count > 0 ? scores.Average() : 0;
                    product.UpdatedAt = DateTime.Now;

                    _context.Products.Update(product);
                }
            }

            if (comboId.HasValue)
            {
                var combo = await _context.Combos.FirstOrDefaultAsync(c => c.Id == comboId.Value);
                if (combo != null)
                {
                    var scores = await _context.Ratings
                        .Where(r => !r.IsDeleted && r.ComboId == combo.Id)
                        .Select(r => r.Score)
                        .ToListAsync();

                    combo.TotalEvaluate = scores.Count;
                    combo.AverageEvaluate = scores.Count > 0 ? scores.Average() : 0;
                    combo.UpdatedAt = DateTime.Now;

                    _context.Combos.Update(combo);
                }
            }
        }
    }
}
