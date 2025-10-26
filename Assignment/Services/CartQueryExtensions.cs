using Assignment.Models;
using Assignment.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;

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
                        .ThenInclude(p => p.ProductTypes)
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.ProductTypeSelections)
                        .ThenInclude(selection => selection.ProductType)
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Combo)
                .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

            if (cart == null || cart.CartItems == null)
            {
                return cart;
            }

            var items = cart.CartItems.ToList();

            foreach (var item in items)
            {
                item.Product?.RefreshDerivedFields();
            }
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
            if (productUnavailable)
            {
                return true;
            }

            if (cartItem.ProductId.HasValue && (cartItem.ProductTypeSelections == null || !cartItem.ProductTypeSelections.Any()))
            {
                return true;
            }

            if (cartItem.ProductTypeSelections != null)
            {
                var invalidSelections = cartItem.ProductTypeSelections
                    .Any(selection => selection.ProductType == null || selection.ProductType.IsDeleted || !selection.ProductType.IsPublish);

                if (invalidSelections)
                {
                    return true;
                }
            }

            var comboUnavailable = cartItem.ComboId.HasValue && (cartItem.Combo == null || cartItem.Combo.IsDeleted || !cartItem.Combo.IsPublish);
            return productUnavailable || comboUnavailable;
        }
    }
}
