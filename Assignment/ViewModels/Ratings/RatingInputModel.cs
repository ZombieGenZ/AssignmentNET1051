using System.ComponentModel.DataAnnotations;

namespace Assignment.ViewModels.Ratings
{
    public class RatingInputModel
    {
        [Required]
        public long OrderItemId { get; set; }

        [Required]
        [Range(1, 5)]
        public int Score { get; set; }

        [StringLength(2000)]
        public string? Comment { get; set; }

        public string? ReturnUrl { get; set; }
    }
}
