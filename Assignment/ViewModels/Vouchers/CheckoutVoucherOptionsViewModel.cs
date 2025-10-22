using System;
using System.Collections.Generic;
using Assignment.Enums;

namespace Assignment.ViewModels.Vouchers
{
    public class CheckoutVoucherOptionViewModel
    {
        public long Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public VoucherType Type { get; set; }
        public VoucherDiscountType DiscountType { get; set; }
        public double Discount { get; set; }
        public bool UnlimitedPercentageDiscount { get; set; }
        public double? MaximumPercentageReduction { get; set; }
        public double MinimumRequirements { get; set; }
        public double PotentialDiscount { get; set; }
        public bool IsSaved { get; set; }
        public bool IsLifeTime { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public long Quantity { get; set; }
        public long Used { get; set; }
        public bool HasCombinedUsageLimit { get; set; }
        public int? MaxCombinedUsageCount { get; set; }
        public bool IsForNewUsersOnly { get; set; }
        public string Group { get; set; } = string.Empty;
    }

    public class CheckoutVoucherOptionsViewModel
    {
        public IReadOnlyCollection<CheckoutVoucherOptionViewModel> PrivateVouchers { get; init; }
            = Array.Empty<CheckoutVoucherOptionViewModel>();

        public IReadOnlyCollection<CheckoutVoucherOptionViewModel> SavedVouchers { get; init; }
            = Array.Empty<CheckoutVoucherOptionViewModel>();
    }
}
