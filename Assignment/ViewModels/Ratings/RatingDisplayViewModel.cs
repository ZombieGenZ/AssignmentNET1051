using System;

namespace Assignment.ViewModels.Ratings
{
    public class RatingDisplayViewModel
    {
        public long Id { get; set; }
        public int Score { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public long OrderItemId { get; set; }
        public string? UserName { get; set; }

        public bool IsEdited => UpdatedAt.HasValue && UpdatedAt.Value > CreatedAt;
    }
}
