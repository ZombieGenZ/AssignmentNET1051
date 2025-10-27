using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Assignment.Models
{
    public class RecipeDetail : BaseEntity
    {
        [ForeignKey(nameof(Recipe))]
        public long RecipeId { get; set; }

        public Recipe? Recipe { get; set; }

        [ForeignKey(nameof(Material))]
        public long MaterialId { get; set; }

        public Material? Material { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Range(0.0001, double.MaxValue)]
        public decimal Quantity { get; set; }

        [ForeignKey(nameof(Unit))]
        public long UnitId { get; set; }

        public Unit? Unit { get; set; }
    }
}
