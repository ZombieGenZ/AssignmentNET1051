using System.ComponentModel.DataAnnotations;

namespace Assignment.Models
{
    public class VoucherUser : BaseEntity
    {
        [Required]
        public long VoucherId { get; set; }
        public virtual Voucher? Voucher { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;
        public virtual ApplicationUser? User { get; set; }
    }
}
