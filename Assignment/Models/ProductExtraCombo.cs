using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Assignment.Models
{
    [Index(nameof(ProductExtraId), nameof(ComboId), IsUnique = true)]
    public class ProductExtraCombo : BaseEntity
    {
        [Required]
        public long ProductExtraId { get; set; }
        public virtual ProductExtra? ProductExtra { get; set; }

        [Required]
        public long ComboId { get; set; }
        public virtual Combo? Combo { get; set; }
    }
}
