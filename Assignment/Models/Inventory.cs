using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Assignment.Models
{
    public class Inventory : BaseEntity
    {
        [Required]
        [ForeignKey(nameof(Material))]
        public long MaterialId { get; set; }

        public Material? Material { get; set; }

        public long? WarehouseId { get; set; }

        public Warehouse? Warehouse { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Range(0, double.MaxValue)]
        public decimal CurrentStock { get; set; }

        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
