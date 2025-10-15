using System;
using Assignment.Enums;
using Assignment.Models;

namespace Assignment.Services
{
    public static class PriceCalculator
    {
        public static double GetProductFinalPrice(Product product)
        {
            if (product == null)
            {
                return 0;
            }

            double price = product.Price;

            if (product.DiscountType == DiscountType.Percent && product.Discount.HasValue)
            {
                price -= price * product.Discount.Value / 100.0;
            }
            else if (product.DiscountType == DiscountType.FixedAmount && product.Discount.HasValue)
            {
                price = product.Discount.Value;
            }

            return Math.Max(price, 0);
        }
    }
}
