using Microsoft.EntityFrameworkCore;

namespace Assignment.Models
{
    [Index(nameof(VoucherId), nameof(ComboId), IsUnique = true)]
    public class VoucherCombo : BaseEntity
    {
        public long VoucherId { get; set; }
        public virtual Voucher? Voucher { get; set; }
        public long ComboId { get; set; }
        public virtual Combo? Combo { get; set; }
    }
}
