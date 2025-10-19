using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Assignment.Models
{
    [Index(nameof(OrderItemId), nameof(UserId), IsUnique = true)]
    public class Rating : BaseEntity
    {
        [Required]
        public string UserId { get; set; } = string.Empty;
        public virtual ApplicationUser? User { get; set; }

        [Required]
        public long OrderItemId { get; set; }
        public virtual OrderItem? OrderItem { get; set; }

        public long? ProductId { get; set; }
        public virtual Product? Product { get; set; }

        public long? ComboId { get; set; }
        public virtual Combo? Combo { get; set; }

        [Required]
        [Range(1, 5)]
        public int Score { get; set; }

        [StringLength(2000)]
        public string? Comment { get; set; }
    }
}
