using Assignment.Models;
using System;
using System.Collections.Generic;
using System.Linq;
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

            product.RefreshDerivedFields();

            var availableTypes = product.ProductTypes?
                .Where(pt => !pt.IsDeleted)
                .ToList()
                ?? new List<ProductType>();

            if (!availableTypes.Any())
            {
                return 0;
            }

            var publishedTypes = availableTypes
                .Where(pt => pt.IsPublish)
                .ToList();

            var priceCandidates = publishedTypes.Any() ? publishedTypes : availableTypes;

            double minFinalPrice = double.MaxValue;

            foreach (var type in priceCandidates)
            {
                var discountValue = type.Discount.HasValue ? (double?)type.Discount.Value : null;
                var finalPrice = ApplyDiscount((double)type.Price, type.DiscountType, discountValue);

                if (finalPrice < minFinalPrice)
                {
                    minFinalPrice = finalPrice;
                }
            }

            if (minFinalPrice == double.MaxValue)
            {
                return 0;
            }

            return Math.Round(Math.Max(minFinalPrice, 0), 2);
        }

        public static double GetProductTypeFinalPrice(ProductType? productType)
        {
            if (productType == null)
            {
                return 0;
            }

            var discountValue = productType.Discount.HasValue ? (double?)productType.Discount.Value : null;
            var unitPrice = ApplyDiscount((double)productType.Price, productType.DiscountType, discountValue);
            return Math.Round(Math.Max(unitPrice, 0), 2);
        }

        public static double GetComboBasePrice(IEnumerable<(Product? product, ProductType? productType, long quantity)> items)
        {
            if (items == null)
            {
                return 0;
            }

            double total = 0;

            foreach (var (product, productType, quantity) in items)
            {
                if (product == null || quantity <= 0)
                {
                    continue;
                }

                double productPrice;

                if (productType != null)
                {
                    productPrice = GetProductTypeFinalPrice(productType);
                }
                else
                {
                    productPrice = GetProductFinalPrice(product);
                }

                total += productPrice * quantity;
            }

            return Math.Round(Math.Max(total, 0), 2);
        }

        public static double GetComboBasePrice(IEnumerable<(Product? product, long quantity)> items)
        {
            var normalized = items?.Select(item => (item.product, (ProductType?)null, item.quantity))
                ?? Array.Empty<(Product?, ProductType?, long)>();

            return GetComboBasePrice(normalized);
        }

        public static double ApplyDiscount(double price, DiscountType discountType, double? discountValue)
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

            var discountValue = combo.Discount.HasValue ? (double?)combo.Discount.Value : null;
            return ApplyDiscount(combo.Price, combo.DiscountType, discountValue);
        }
    }
}
