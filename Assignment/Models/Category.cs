using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Assignment.Models
{
    public class Category : BaseEntity
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; }
        [Required]
        [Range(0, long.MaxValue)]
        public long Index { get; set; }
        public virtual IEnumerable<Product>? Products { get; set; }
    }
}
