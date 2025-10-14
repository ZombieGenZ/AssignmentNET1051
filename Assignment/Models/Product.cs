using Assignment.Enums;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Assignment.Models
{
    public class Product : BaseEntity
    {
        [Required]
        [StringLength(500)]
        public string Name { get; set; }
        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string Description { get; set; }
        [Required]
        [Range(0, double.MaxValue)]
        public double Price { get; set; }
        [Required]
        [Range(0, long.MaxValue)]
        public long Stock { get; set; }
        [Required]
        [Range(0, long.MaxValue)]
        [DefaultValue(0)]
        public long Sold { get; set; }
        [Required]
        [DefaultValue(DiscountType.None)]
        public DiscountType DiscountType { get; set; }
        [Range(0, long.MaxValue)]
        public long? Discount { get; set; }
        [Required]
        public bool IsPublish { get; set; }
        [Required]
        [StringLength(1000)]
        [DataType(DataType.Url)]
        public string ProductImageUrl { get; set; }
        [Required]
        [Range(0, long.MaxValue)]
        public long PreparationTime { get; set; }
        [Required]
        [Range(0, long.MaxValue)]
        public long Calories { get; set; }
        [Required]
        [StringLength(1000)]
        public string Ingredients { get; set; }
        [Required]
        [DefaultValue(false)]
        public bool IsSpicy { get; set; }
        [Required]
        [DefaultValue(false)]
        public bool IsVegetarian { get; set; }
        [Required]
        [Range(0, long.MaxValue)]
        public long TotalEvaluate { get; set; } = 0;
        [Required]
        [Range(0, 5)]
        public double AverageEvaluate { get; set; } = 0;
        public long CategoryId { get; set; }
        public virtual Category? Category { get; set; }
    }
}
