namespace Assignment.Models
{
    public class Cart : BaseEntity
    {
        public virtual IEnumerable<CartItem> CartItems { get; set; }
        public string? UserId { get; set; }
    }
}
