using System;

namespace Assignment.ViewModels.Ratings
{
    public class RatingItemViewModel
    {
        public long OrderItemId { get; set; }
        public long OrderId { get; set; }
        public DateTime OrderCreatedAt { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public long Quantity { get; set; }
        public double Price { get; set; }
        public bool IsAvailable { get; set; }
        public int? Score { get; set; }
        public string? Comment { get; set; }
        public bool CanRate { get; set; }
        public long? ProductId { get; set; }
        public long? ComboId { get; set; }
    }
}
