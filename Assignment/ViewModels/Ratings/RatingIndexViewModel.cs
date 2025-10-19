using System.Collections.Generic;

namespace Assignment.ViewModels.Ratings
{
    public class RatingIndexViewModel
    {
        public IReadOnlyCollection<RatingItemViewModel> Items { get; set; } = new List<RatingItemViewModel>();
    }
}
