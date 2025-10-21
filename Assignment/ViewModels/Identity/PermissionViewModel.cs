using System;

namespace Assignment.ViewModels.Identity
{
    public class PermissionViewModel
    {
        public string Group { get; set; } = string.Empty;

        public string Key { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public bool IsSelected { get; set; }

        public bool IsInherited { get; set; }

        public PermissionViewModel Clone()
        {
            return new PermissionViewModel
            {
                Group = Group,
                Key = Key,
                DisplayName = DisplayName,
                IsSelected = IsSelected,
                IsInherited = IsInherited,
            };
        }
    }
}
