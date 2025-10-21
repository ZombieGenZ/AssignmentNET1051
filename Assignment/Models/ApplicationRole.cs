using System;
using Microsoft.AspNetCore.Identity;

namespace Assignment.Models
{
    public class ApplicationRole : IdentityRole
    {
        public bool IsDeleted { get; set; }

        public string? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? UpdatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
