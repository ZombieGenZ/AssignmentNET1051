using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Assignment.Models
{
    [Index(nameof(ProductExtraId), nameof(ProductId), IsUnique = true)]
    public class ProductExtraProduct : BaseEntity
    {
        [Required]
        public long ProductExtraId { get; set; }
        public virtual ProductExtra? ProductExtra { get; set; }

        [Required]
        public long ProductId { get; set; }
        public virtual Product? Product { get; set; }
    }
}
