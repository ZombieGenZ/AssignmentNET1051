using System;
using Assignment.Enums;
using Microsoft.AspNetCore.Identity;

namespace Assignment.Models
{
    public class ApplicationUser : IdentityUser
    {
        public ApplicationUser()
        {
            SecurityStamp ??= Guid.NewGuid().ToString();
        }

        public string? FullName { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public long Exp { get; set; }
        public long Point { get; set; }
        public long TotalPoint { get; set; }
        public CustomerRank Rank { get; set; } = CustomerRank.Potential;
        public bool ExcludeFromLeaderboard { get; set; }
        public decimal Booster { get; set; } = 1m;
    }

}
