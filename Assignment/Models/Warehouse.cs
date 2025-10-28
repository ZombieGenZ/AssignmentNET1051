using System.ComponentModel.DataAnnotations;

namespace Assignment.Models
{
    public class Warehouse : BaseEntity
    {
        [Required]
        [StringLength(100)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string Name { get; set; } = string.Empty;

        [StringLength(255)]
        public string? ContactName { get; set; }

        [StringLength(50)]
        public string? PhoneNumber { get; set; }

        [StringLength(255)]
        [EmailAddress]
        public string? Email { get; set; }

        [StringLength(500)]
        public string? Address { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }
    }
}
