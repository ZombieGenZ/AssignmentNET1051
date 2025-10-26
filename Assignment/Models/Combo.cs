using Assignment.Enums;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Assignment.Models
{
    public class Combo : BaseEntity
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
        public long Index { get; set; }
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
        public string ImageUrl { get; set; }
        [Required]
        [Range(0, long.MaxValue)]
        public long TotalEvaluate { get; set; } = 0;
        [Required]
        [Range(0, 5)]
        public double AverageEvaluate { get; set; } = 0;
        public virtual IEnumerable<ComboItem>? ComboItems { get; set; }

        [NotMapped]
        public bool HasProductDiscount { get; private set; }

        [NotMapped]
        public bool HasOwnDiscount => DiscountType != Enums.DiscountType.None && Discount.HasValue;

        [NotMapped]
        public bool HasAnyDiscount => HasOwnDiscount || HasProductDiscount;

        public void RefreshDerivedFields()
        {
            foreach (var item in ComboItems ?? Enumerable.Empty<ComboItem>())
            {
                item.Product?.RefreshDerivedFields();
            }

            HasProductDiscount = (ComboItems ?? Enumerable.Empty<ComboItem>())
                .Any(ci => ci.Product?.HasDiscount == true);
        }
    }
}
