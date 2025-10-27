using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Assignment.Enums;

namespace Assignment.Models
{
    public class ReceivingNote : BaseEntity
    {
        [Required]
        [StringLength(100)]
        public string NoteNumber { get; set; } = string.Empty;

        [Column(TypeName = "date")]
        public DateTime Date { get; set; } = DateTime.UtcNow.Date;

        [StringLength(100)]
        public string? SupplierId { get; set; }

        [StringLength(255)]
        public string? SupplierName { get; set; }

        public long? WarehouseId { get; set; }

        [Required]
        public ReceivingNoteStatus Status { get; set; } = ReceivingNoteStatus.Draft;

        public bool IsStockApplied { get; set; }

        public DateTime? CompletedAt { get; set; }

        public ICollection<ReceivingDetail> Details { get; set; } = new List<ReceivingDetail>();
    }
}
