using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Assignment.Models
{
    [Index(nameof(RewardId), nameof(ComboId), IsUnique = true)]
    public class RewardCombo : BaseEntity
    {
        [Required]
        public long RewardId { get; set; }
        public virtual Reward? Reward { get; set; }

        [Required]
        public long ComboId { get; set; }
        public virtual Combo? Combo { get; set; }
    }
}
