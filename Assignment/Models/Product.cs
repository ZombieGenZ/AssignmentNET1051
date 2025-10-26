using Assignment.Enums;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Linq;

namespace Assignment.Models
{
    public class Product : BaseEntity
    {
        [Required]
        [StringLength(500)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string Description { get; set; } = string.Empty;

        [Required]
        public bool IsPublish { get; set; }

        [Required]
        [StringLength(1000)]
        [DataType(DataType.Url)]
        public string ProductImageUrl { get; set; } = string.Empty;

        [Required]
        [DefaultValue(false)]
        public bool IsSpicy { get; set; }

        [Required]
        [DefaultValue(false)]
        public bool IsVegetarian { get; set; }

        [Required]
        [Range(0, long.MaxValue)]
        public long TotalEvaluate { get; set; } = 0;

        [Required]
        [Range(0, 5)]
        public double AverageEvaluate { get; set; } = 0;

        public long CategoryId { get; set; }

        public virtual Category? Category { get; set; }

        public virtual ICollection<ProductType> ProductTypes { get; set; } = new List<ProductType>();

        [NotMapped]
        public string PriceRange { get; private set; } = string.Empty;

        [NotMapped]
        public int TotalStock { get; private set; }

        [NotMapped]
        public int TotalSold { get; private set; }

        [NotMapped]
        public int MinPreparationTime { get; private set; }

        [NotMapped]
        public int MinCalories { get; private set; }

        [NotMapped]
        public string CombinedIngredients { get; private set; } = string.Empty;

        [NotMapped]
        public bool HasDiscount { get; private set; }

        [NotMapped]
        public ProductType? PrimaryProductType { get; private set; }

        public void RefreshDerivedFields()
        {
            var activeTypes = (ProductTypes ?? Enumerable.Empty<ProductType>())
                .Where(pt => !pt.IsDeleted)
                .ToList();

            IsSpicy = activeTypes.Any(pt => pt.IsSpicy);
            IsVegetarian = activeTypes.Any(pt => pt.IsVegetarian);

            var publishedTypes = activeTypes
                .Where(pt => pt.IsPublish)
                .ToList();

            IsPublish = publishedTypes.Any();

            PrimaryProductType = publishedTypes
                .OrderBy(pt => pt.Price)
                .FirstOrDefault()
                ?? activeTypes
                    .OrderBy(pt => pt.Price)
                    .FirstOrDefault();

            TotalStock = activeTypes.Any() ? activeTypes.Max(pt => pt.Stock) : 0;
            TotalSold = activeTypes.Sum(pt => pt.Sold);

            var priceSourceTypes = publishedTypes.Any() ? publishedTypes : activeTypes;

            if (priceSourceTypes.Any())
            {
                HasDiscount = priceSourceTypes.Any(pt =>
                    pt.DiscountType != DiscountType.None && (pt.Discount ?? 0) > 0);

                var minPrice = priceSourceTypes.Min(pt => pt.Price);
                var maxPrice = priceSourceTypes.Max(pt => pt.Price);
                PriceRange = minPrice == maxPrice
                    ? FormatPrice(minPrice)
                    : $"{FormatPrice(minPrice)} - {FormatPrice(maxPrice)}";

                MinPreparationTime = priceSourceTypes.Min(pt => pt.PreparationTime);
                MinCalories = priceSourceTypes.Min(pt => pt.Calories);
                CombinedIngredients = string.Join(", ", priceSourceTypes
                    .Select(pt => pt.Ingredients)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase));
            }
            else
            {
                PriceRange = string.Empty;
                MinPreparationTime = 0;
                MinCalories = 0;
                CombinedIngredients = string.Empty;
                HasDiscount = false;
            }
        }

        private static string FormatPrice(decimal price)
        {
            return price.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
