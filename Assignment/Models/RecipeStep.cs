using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Assignment.Models
{
    public class RecipeStep : BaseEntity
    {
        [ForeignKey(nameof(Recipe))]
        public long RecipeId { get; set; }

        public Recipe? Recipe { get; set; }

        [Range(1, int.MaxValue)]
        public int StepOrder { get; set; }

        [Required]
        [StringLength(2000)]
        public string Description { get; set; } = string.Empty;
    }
}
