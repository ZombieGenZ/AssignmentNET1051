using System.ComponentModel.DataAnnotations;

namespace Assignment.ViewModels.Cart
{
    public class ProductTypeSelectionRequest
    {
        [Required]
        public long ProductTypeId { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }
    }
}
