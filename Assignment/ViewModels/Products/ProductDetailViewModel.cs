using Assignment.Models;
using Assignment.ViewModels.Ratings;
using System.Collections.Generic;

namespace Assignment.ViewModels.Products
{
    public class ProductDetailViewModel
    {
        public Product Product { get; set; } = null!;
        public RatingDisplayViewModel? UserRating { get; set; }
        public bool CanRate { get; set; }
        public long? OrderItemIdForRating { get; set; }
        public bool CanDeleteRating { get; set; }
        public IEnumerable<RatingDisplayViewModel> Ratings { get; set; } = new List<RatingDisplayViewModel>();
        public IDictionary<int, int> RatingCounts { get; set; } = new Dictionary<int, int>();
        public int? SelectedRatingFilter { get; set; }
    }
}
