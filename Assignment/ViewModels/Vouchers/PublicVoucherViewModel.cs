using System;
using Assignment.Enums;

namespace Assignment.ViewModels.Vouchers
{
    public class PublicVoucherViewModel
    {
        public long Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public VoucherProductScope ProductScope { get; set; }
        public VoucherDiscountType DiscountType { get; set; }
        public double Discount { get; set; }
        public bool UnlimitedPercentageDiscount { get; set; }
        public double? MaximumPercentageReduction { get; set; }
        public double MinimumRequirements { get; set; }
        public long Quantity { get; set; }
        public long Used { get; set; }
        public bool IsLifeTime { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public bool IsSaved { get; set; }

        public bool IsAvailable => Quantity <= 0 || Used < Quantity;

        public bool IsActive(DateTime referenceTime)
        {
            if (StartTime > referenceTime)
            {
                return false;
            }

            if (!IsLifeTime && EndTime.HasValue && EndTime.Value < referenceTime)
            {
                return false;
            }

            return true;
        }
    }
}
