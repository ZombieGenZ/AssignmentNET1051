using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Assignment.Models
{
    public class Recipe : BaseEntity
    {
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        [ForeignKey(nameof(OutputUnit))]
        public long OutputUnitId { get; set; }

        public Unit? OutputUnit { get; set; }

        [Range(0, int.MaxValue)]
        public int PreparationTime { get; set; }

        public virtual ICollection<RecipeDetail> Details { get; set; } = new List<RecipeDetail>();

        public virtual ICollection<RecipeStep> Steps { get; set; } = new List<RecipeStep>();
    }
}
