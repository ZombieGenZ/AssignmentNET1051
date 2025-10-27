using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Assignment.Models
{
    public class ReceivingDetail : BaseEntity
    {
        [Required]
        [ForeignKey(nameof(ReceivingNote))]
        public long ReceivingNoteId { get; set; }

        public ReceivingNote? ReceivingNote { get; set; }

        [Required]
        [ForeignKey(nameof(Material))]
        public long MaterialId { get; set; }

        public Material? Material { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,4)")]
        [Range(0.0001, double.MaxValue)]
        public decimal Quantity { get; set; }

        [Required]
        [ForeignKey(nameof(Unit))]
        public long UnitId { get; set; }

        public Unit? Unit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(0, double.MaxValue)]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Range(0, double.MaxValue)]
        public decimal BaseQuantity { get; set; }
    }
}
