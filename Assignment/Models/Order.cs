using Assignment.Enums;
using System.ComponentModel.DataAnnotations;

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
        public string? VoucherId { get; set; }
        public virtual Voucher? Voucher { get; set; }
        public PaymentMethodType? PaymentMethod { get; set; }
        public PaymentType PaymentType { get; set; }
        [Required]
        public OrderStatus Status { get; set; }

        public virtual IEnumerable<OrderItem>? OrderItems { get; set; }
    }
}
