using System.ComponentModel.DataAnnotations;

namespace Assignment.Models
{
    public class VoucherProduct : BaseEntity
    {
        [Required]
        public long VoucherId { get; set; }
        public virtual Voucher? Voucher { get; set; }

        [Required]
        public long ProductId { get; set; }
        public virtual Product? Product { get; set; }
    }
}
