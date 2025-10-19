using Assignment.Enums;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Assignment.Models
{
    public class Voucher : BaseEntity
    {
        [Required]
        [StringLength(100)]
        public string Code { get; set; }
        [Required]
        [StringLength(300)]
        public string Name { get; set; }
        [Required]
        [StringLength(1000)]
        public string Description { get; set; }
        [Required]
        public VoucherType Type { get; set; }
        [Required]
        [DisplayName("Phạm vi sản phẩm")]
        public VoucherProductScope ProductScope { get; set; } = VoucherProductScope.AllProducts;
        public string? UserId { get; set; }
        [Required]
        [Range(0, double.MaxValue)]
        public double Discount { get; set; }
        [Required]
        public VoucherDiscountType DiscountType { get; set; }
        [Required]
        [Range(0, long.MaxValue)]
        [DefaultValue(0)]
        public long Used { get; set; } = 0;
        [Required]
        [Range(0, long.MaxValue)]
        [DefaultValue(0)]
        public long Quantity { get; set; } = 0;
        [Required]
        public DateTime StartTime { get; set; }
        [Required]
        [DefaultValue(false)]
        public bool IsLifeTime { get; set; } = false;
        public DateTime? EndTime { get; set; }
        [Required]
        [Range(0, double.MaxValue)]
        public double MinimumRequirements { get; set; }
        [Required]
        [DefaultValue(false)]
        public bool UnlimitedPercentageDiscount { get; set; } = false;
        [Range(0, double.MaxValue)]
        public double? MaximumPercentageReduction { get; set; }
        [Required]
        [DefaultValue(false)]
        [DisplayName("Giới hạn số voucher áp dụng chung")]
        public bool HasCombinedUsageLimit { get; set; } = false;
        [Range(1, int.MaxValue)]
        [DisplayName("Số voucher áp dụng chung tối đa")]
        public int? MaxCombinedUsageCount { get; set; }
        public virtual ICollection<VoucherUser>? VoucherUsers { get; set; }
        public virtual ICollection<VoucherProduct>? VoucherProducts { get; set; }
        public virtual ICollection<OrderVoucher>? OrderVouchers { get; set; }
    }
}
