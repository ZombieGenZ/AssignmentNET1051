using System.ComponentModel.DataAnnotations;
using Assignment.Enums;
using System.ComponentModel;

namespace Assignment.Models
{
    public class Reward : BaseEntity
    {
        [Required]
        public RewardItemType Type { get; set; } = RewardItemType.Voucher;

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

        [Range(0, int.MaxValue)]
        public int ValidityValue { get; set; } = 30;

        [Required]
        public RewardValidityUnit ValidityUnit { get; set; } = RewardValidityUnit.Day;

        [Required]
        public bool IsValidityUnlimited { get; set; } = false;

        [Required]
        public bool IsPublish { get; set; } = true;

        [Required]
        public VoucherProductScope VoucherProductScope { get; set; } = VoucherProductScope.AllProducts;

        [Required]
        public VoucherComboScope VoucherComboScope { get; set; } = VoucherComboScope.AllCombos;

        [Required]
        public VoucherDiscountType VoucherDiscountType { get; set; } = VoucherDiscountType.Money;

        [Range(0, double.MaxValue)]
        public double VoucherDiscount { get; set; }

        [Range(0, double.MaxValue)]
        public double VoucherMinimumRequirements { get; set; }

        [Required]
        public bool VoucherUnlimitedPercentageDiscount { get; set; } = false;

        [Range(0, double.MaxValue)]
        public double? VoucherMaximumPercentageReduction { get; set; }

        [Required]
        public bool VoucherHasCombinedUsageLimit { get; set; } = false;

        [Range(1, int.MaxValue)]
        public int? VoucherMaxCombinedUsageCount { get; set; }

        [Required]
        [DefaultValue(false)]
        public bool VoucherIsForNewUsersOnly { get; set; } = false;

        public virtual ICollection<RewardRedemption>? Redemptions { get; set; }
    }
}
