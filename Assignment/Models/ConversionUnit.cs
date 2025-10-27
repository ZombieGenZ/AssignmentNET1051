using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Assignment.Models
{
    public class ConversionUnit : BaseEntity
    {
        [Required]
        [ForeignKey(nameof(FromUnit))]
        public long FromUnitId { get; set; }

        public Unit? FromUnit { get; set; }

        [Required]
        [ForeignKey(nameof(ToUnit))]
        public long ToUnitId { get; set; }

        public Unit? ToUnit { get; set; }

        [Required]
        [Range(0.0000001, double.MaxValue, ErrorMessage = "Giá trị chuyển đổi phải lớn hơn 0.")]
        [Column(TypeName = "decimal(18,6)")]
        public decimal ConversionRate { get; set; }

        [StringLength(255)]
        public string? Description { get; set; }
    }
}
