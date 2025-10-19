using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Assignment.Models
{
    [Index(nameof(VoucherId), nameof(ProductId), IsUnique = true)]
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
