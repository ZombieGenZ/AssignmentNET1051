using System;
using System.Collections.Generic;

namespace Assignment.ViewModels.Identity
{
    public class UserManagementViewModel
    {
        public string Id { get; set; } = string.Empty;

        public string? Email { get; set; }

        public string? UserName { get; set; }

        public string? FullName { get; set; }

        public bool IsLockedOut { get; set; }

        public DateTimeOffset? LockoutEnd { get; set; }

        public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();

        public IReadOnlyList<PermissionViewModel> Permissions { get; set; } = Array.Empty<PermissionViewModel>();

        public bool IsSuperAdmin { get; set; }

        public bool ExcludeFromLeaderboard { get; set; }

        public decimal Booster { get; set; }
    }

    public class UserListItemViewModel
    {
        public string Id { get; set; } = string.Empty;

        public string? Email { get; set; }

        public string? UserName { get; set; }

        public string? FullName { get; set; }

        public bool IsLockedOut { get; set; }

        public DateTimeOffset? LockoutEnd { get; set; }

        public bool IsSuperAdmin { get; set; }

        public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();

        public bool ExcludeFromLeaderboard { get; set; }

        public decimal Booster { get; set; }
    }
}
