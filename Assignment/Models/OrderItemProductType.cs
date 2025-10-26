using System.ComponentModel.DataAnnotations;

namespace Assignment.Models
{
    public class OrderItemProductType : BaseEntity
    {
        [Required]
        public long OrderItemId { get; set; }
        public virtual OrderItem? OrderItem { get; set; }

        [Required]
        public long ProductTypeId { get; set; }
        public virtual ProductType? ProductType { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public double UnitPrice { get; set; }
    }
}
