using System;
using Microsoft.AspNetCore.Identity;

namespace Assignment.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; }
        public DateTime DateOfBirth { get; set; }
    }

}
