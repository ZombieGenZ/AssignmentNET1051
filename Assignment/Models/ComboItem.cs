using System.ComponentModel.DataAnnotations;

namespace Assignment.Models
{
    public class ComboItem : BaseEntity
    {
        public long ComboId { get; set; }
        public virtual Combo? Combo { get; set; }
        public long ProductId { get; set; }
        public virtual Product? Product { get; set; }
        [Required]
        [Range(1, long.MaxValue)]
        public long Quantity { get; set; }
    }
}
