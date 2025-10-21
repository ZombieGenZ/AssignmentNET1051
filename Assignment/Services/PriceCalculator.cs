using Assignment.Models;
using System;
using System.Collections.Generic;
using Assignment.Enums;

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
            else if (product.DiscountType == DiscountType.Amount && product.Discount.HasValue)
            {
                price -= product.Discount.Value;
            }

            return Math.Max(price, 0);
        }

        public static double GetComboBasePrice(IEnumerable<(Product? product, long quantity)> items)
        {
            if (items == null)
            {
                return 0;
            }

            double total = 0;

            foreach (var (product, quantity) in items)
            {
                if (product == null || quantity <= 0)
                {
                    continue;
                }

                var productPrice = GetProductFinalPrice(product);
                total += productPrice * quantity;
            }

            return Math.Round(Math.Max(total, 0), 2);
        }

        public static double ApplyDiscount(double price, DiscountType discountType, long? discountValue)
        {
            var normalizedPrice = Math.Max(price, 0);
            double finalPrice = normalizedPrice;

            if (discountType == DiscountType.Percent && discountValue.HasValue)
            {
                finalPrice = normalizedPrice - normalizedPrice * discountValue.Value / 100.0;
            }
            else if (discountType == DiscountType.FixedAmount && discountValue.HasValue)
            {
                finalPrice = discountValue.Value;
            }
            else if (discountType == DiscountType.Amount && discountValue.HasValue)
            {
                finalPrice = normalizedPrice - discountValue.Value;
            }

            return Math.Round(Math.Max(finalPrice, 0), 2);
        }

        public static double GetComboFinalPrice(Combo? combo)
        {
            if (combo == null)
            {
                return 0;
            }

            return ApplyDiscount(combo.Price, combo.DiscountType, combo.Discount);
        }
    }
}
