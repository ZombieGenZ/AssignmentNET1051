using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Assignment.ViewModels.Cart
{
    public class AddProductToCartRequest
    {
        [Required]
        public long ProductId { get; set; }

        [Required]
        [MinLength(1)]
        public List<ProductTypeSelectionRequest> Selections { get; set; } = new();
    }
}
