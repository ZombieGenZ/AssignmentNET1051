using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Assignment.Models
{
    public class Material : BaseEntity
    {
        [Required]
        [StringLength(100)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [ForeignKey(nameof(Unit))]
        public long UnitId { get; set; }

        public Unit? Unit { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Range(0, double.MaxValue)]
        public decimal MinStockLevel { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(0, double.MaxValue)]
        public decimal Price { get; set; }
    }
}
