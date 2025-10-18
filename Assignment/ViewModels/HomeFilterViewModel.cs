using System;

namespace Assignment.ViewModels
{
    public class HomeFilterViewModel
    {
        private static readonly string[] AllowedSegments = new[] { "all", "combo", "category" };

        public string Segment { get; set; } = "all";

        public string? SearchTerm { get; set; }

        public long? CategoryId { get; set; }

        public decimal? MinPrice { get; set; }

        public decimal? MaxPrice { get; set; }

        public bool? IsSpicy { get; set; }

        public bool? IsVegetarian { get; set; }

        public bool OnlyDiscounted { get; set; }

        public bool HasAdvancedFilters =>
            MinPrice.HasValue ||
            MaxPrice.HasValue ||
            IsSpicy.HasValue ||
            IsVegetarian.HasValue ||
            OnlyDiscounted;

        public void Normalize()
        {
            Segment = string.IsNullOrWhiteSpace(Segment)
                ? "all"
                : Segment.Trim().ToLowerInvariant();

            if (Array.IndexOf(AllowedSegments, Segment) < 0)
            {
                Segment = "all";
            }
        }
    }
}
