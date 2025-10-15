using Assignment.Data;
using Assignment.Models;
using Microsoft.EntityFrameworkCore;

namespace Assignment.Services
{
    public static class CartQueryExtensions
    {
        public static async Task<Cart?> LoadCartWithAvailableItemsAsync(this ApplicationDbContext context, string? userId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            var cart = await context.Carts
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Product)
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Combo)
                .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

            if (cart == null || cart.CartItems == null)
            {
                return cart;
            }

            var items = cart.CartItems.ToList();
            var unavailableItems = items
                .Where(ci => IsUnavailable(ci))
                .ToList();

            if (unavailableItems.Any())
            {
                context.CartItems.RemoveRange(unavailableItems);
                await context.SaveChangesAsync(cancellationToken);
            }

            cart.CartItems = items.Except(unavailableItems).ToList();
            return cart;
        }

        public static bool IsUnavailable(CartItem cartItem)
        {
            var productUnavailable = cartItem.ProductId.HasValue && (cartItem.Product == null || cartItem.Product.IsDeleted || !cartItem.Product.IsPublish);
            var comboUnavailable = cartItem.ComboId.HasValue && (cartItem.Combo == null || cartItem.Combo.IsDeleted || !cartItem.Combo.IsPublish);
            return productUnavailable || comboUnavailable;
        }
    }
}
