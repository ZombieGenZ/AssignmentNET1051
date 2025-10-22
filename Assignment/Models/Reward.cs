using System.ComponentModel.DataAnnotations;
using Assignment.Enums;

namespace Assignment.Models
{
    public class Reward : BaseEntity
    {
        [Required]
        [StringLength(300)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(1000)]
        public string Description { get; set; } = string.Empty;

        [Range(0, long.MaxValue)]
        public long Quantity { get; set; }

        [Range(0, long.MaxValue)]
        public long Redeemed { get; set; }

        [Range(0, long.MaxValue)]
        public long PointCost { get; set; }

        public CustomerRank? MinimumRank { get; set; }

        [Range(1, int.MaxValue)]
        public int ValidityValue { get; set; } = 30;

        [Required]
        public RewardValidityUnit ValidityUnit { get; set; } = RewardValidityUnit.Day;

        [Required]
        public bool IsPublish { get; set; } = true;

        public virtual ICollection<RewardRedemption>? Redemptions { get; set; }
    }
}
