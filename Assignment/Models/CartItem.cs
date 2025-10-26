using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Assignment.Models
{
    public class CartItem : BaseEntity
    {
        public long CartId { get; set; }
        public virtual Cart? Cart { get; set; }
        public long? ComboId { get; set; }
        public virtual Combo? Combo { get; set; }

        public long? ProductId { get; set; }
        public virtual Product? Product { get; set; }

        [Required]
        [Range(1, long.MaxValue)]
        public long Quantity { get; set; }

        public virtual ICollection<CartItemProductType> ProductTypeSelections { get; set; } = new List<CartItemProductType>();
    }
}
