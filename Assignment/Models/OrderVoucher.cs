namespace Assignment.Models
{
    public class OrderVoucher : BaseEntity
    {
        public long OrderId { get; set; }
        public virtual Order? Order { get; set; }
        public long VoucherId { get; set; }
        public virtual Voucher? Voucher { get; set; }
        public double DiscountAmount { get; set; }
    }
}
