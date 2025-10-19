using Assignment.Models;
using Assignment.ViewModels.Ratings;

namespace Assignment.ViewModels.Products
{
    public class ProductDetailViewModel
    {
        public Product Product { get; set; } = null!;
        public RatingDisplayViewModel? UserRating { get; set; }
        public bool CanRate { get; set; }
        public long? OrderItemIdForRating { get; set; }
        public bool CanDeleteRating { get; set; }
    }
}
