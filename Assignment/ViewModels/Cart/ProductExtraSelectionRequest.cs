using System.ComponentModel.DataAnnotations;

namespace Assignment.ViewModels.Cart
{
    public class ProductExtraSelectionRequest
    {
        [Required]
        public long ProductExtraId { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }
    }
}
