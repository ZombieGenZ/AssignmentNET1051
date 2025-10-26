using Assignment.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Assignment.Models
{
    public class Order : BaseEntity
    {
        [Required]
        [StringLength(200)]
        public string Name { get; set; }
        [StringLength(200)]
        public string? Email { get; set; }
        [Required]
        [StringLength(11)]
        public string Phone { get; set; }
        [Required]
        [Range(0, long.MaxValue)]
        public long TotalQuantity { get; set; }
        [Required]
        [Range(0, double.MaxValue)]
        public double TotalPrice { get; set; }
        [Required]
        [Range(0, double.MaxValue)]
        public double Discount { get; set; }
        [Required]
        [Range(0, double.MaxValue)]
        public double Vat { get; set; }
        [Required]
        [Range(0, double.MaxValue)]
        public double TotalBill { get; set; }
        public string? Note { get; set; }
        public string? UserId { get; set; }
        [NotMapped]
        public long? VoucherId
        {
            get
            {
                var trimmedValue = VoucherIdStorage?.Trim();
                if (string.IsNullOrEmpty(trimmedValue))
                {
                    return null;
                }

                if (long.TryParse(trimmedValue, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsedValue))
                {
                    return parsedValue;
                }

                return null;
            }
            set
            {
                VoucherIdStorage = value?.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        [Column("VoucherId")]
        [System.Text.Json.Serialization.JsonIgnore]
        public string? VoucherIdStorage { get; set; }
        public virtual ICollection<OrderVoucher>? OrderVouchers { get; set; }
        public PaymentMethodType? PaymentMethod { get; set; }
        public PaymentType PaymentType { get; set; }
        [Required]
        public OrderStatus Status { get; set; }

        [Required]
        public bool LoyaltyRewardsApplied { get; set; } = false;

        public virtual IEnumerable<OrderItem>? OrderItems { get; set; }

        [NotMapped]
        public string? SelectedCartItemIds { get; set; }
        [NotMapped]
        public string? SelectedCartSelectionIds { get; set; }
        [NotMapped]
        public List<long> AppliedVoucherIds { get; set; } = new();
    }
}
