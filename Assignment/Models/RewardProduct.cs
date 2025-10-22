using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Assignment.Models
{
    [Index(nameof(RewardId), nameof(ProductId), IsUnique = true)]
    public class RewardProduct : BaseEntity
    {
        [Required]
        public long RewardId { get; set; }
        public virtual Reward? Reward { get; set; }

        [Required]
        public long ProductId { get; set; }
        public virtual Product? Product { get; set; }
    }
}
