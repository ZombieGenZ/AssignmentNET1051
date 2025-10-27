using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Assignment.Models
{
    public class Unit : BaseEntity
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Description { get; set; }

        public virtual ICollection<ConversionUnit> Conversions { get; set; } = new List<ConversionUnit>();

        public virtual ICollection<ConversionUnit> ConvertedFrom { get; set; } = new List<ConversionUnit>();

        public virtual ICollection<Material> Materials { get; set; } = new List<Material>();
    }
}
