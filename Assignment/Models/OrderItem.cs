using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Assignment.Models
{
    public class OrderItem : BaseEntity
    {
        public long OrderId { get; set; }
        public virtual Order Order { get; set; }
        [Required]
        [Range(0, double.MaxValue)]
        public double Price { get; set; }
        [Required]
        [Range(0, long.MaxValue)]
        public long Quantity { get; set; }
        public long? ComboId { get; set; }
        public virtual Combo? Combo { get; set; }
        public long? ProductId { get; set; }
        public virtual Product? Product { get; set; }

        public virtual ICollection<OrderItemProductType> ProductTypeSelections { get; set; } = new List<OrderItemProductType>();
        public virtual ICollection<OrderItemProductExtra> ProductExtraSelections { get; set; } = new List<OrderItemProductExtra>();
    }
}
