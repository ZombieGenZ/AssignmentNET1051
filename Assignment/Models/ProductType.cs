using Assignment.Enums;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Assignment.Models
{
    public class ProductType : BaseEntity
    {
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000)]
        [DataType(DataType.Url)]
        public string? ProductTypeImageUrl { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Range(typeof(decimal), "0", "79228162514264337593543950335")]
        public decimal Price { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int Stock { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        [DefaultValue(0)]
        public int Sold { get; set; } = 0;

        [Required]
        [DefaultValue(DiscountType.None)]
        public DiscountType DiscountType { get; set; } = DiscountType.None;

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Discount { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int PreparationTime { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int Calories { get; set; }

        [Required]
        [StringLength(2000)]
        public string Ingredients { get; set; } = string.Empty;

        [Required]
        [DefaultValue(false)]
        public bool IsSpicy { get; set; } = false;

        [Required]
        [DefaultValue(false)]
        public bool IsVegetarian { get; set; } = false;

        [Required]
        [DefaultValue(true)]
        public bool IsPublish { get; set; } = true;

        [Required]
        public long ProductId { get; set; }

        public virtual Product? Product { get; set; }
    }
}
