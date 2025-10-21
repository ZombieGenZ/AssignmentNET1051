using System;
using System.Collections.Generic;

namespace Assignment.ViewModels.Identity
{
    public class RoleManagementViewModel
    {
        public string? Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public bool IsDeleted { get; set; }

        public string? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; }

        public string? UpdatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public int AssignedUserCount { get; set; }

        public IReadOnlyList<PermissionViewModel> Permissions { get; set; } = Array.Empty<PermissionViewModel>();
    }

    public class RoleListItemViewModel
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public bool IsDeleted { get; set; }

        public int AssignedUserCount { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
