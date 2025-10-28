using Assignment.Enums;
using System;

namespace Assignment.ViewModels.ProductExtras
{
    public class ProductExtraDisplayViewModel
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public decimal Price { get; set; }
        public DiscountType DiscountType { get; set; }
        public decimal? Discount { get; set; }
        public decimal FinalPrice { get; set; }
        public bool IsSpicy { get; set; }
        public bool IsVegetarian { get; set; }
        public string Ingredients { get; set; } = string.Empty;
    }
}
