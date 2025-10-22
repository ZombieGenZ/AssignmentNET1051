using System.ComponentModel.DataAnnotations;

namespace Assignment.Models
{
    public class RewardRedemption : BaseEntity
    {
        [Required]
        public long RewardId { get; set; }

        public Reward? Reward { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser? User { get; set; }

        public long? VoucherId { get; set; }

        public Voucher? Voucher { get; set; }

        [Required]
        [StringLength(100)]
        public string Code { get; set; } = string.Empty;

        [Required]
        public DateTime ValidFrom { get; set; }

        public DateTime? ValidTo { get; set; }

        public bool IsUsed { get; set; }

        public DateTime? UsedAt { get; set; }

        [Range(0, long.MaxValue)]
        public long PointCost { get; set; }
    }
}
