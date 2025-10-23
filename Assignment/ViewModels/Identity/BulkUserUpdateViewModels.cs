using System;
using System.Collections.Generic;

namespace Assignment.ViewModels.Identity
{
    public class BulkUserUpdateRequest
    {
        public IReadOnlyCollection<string> UserIds { get; set; } = Array.Empty<string>();

        public bool ApplyRoles { get; set; }

        public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();

        public bool ApplyPermissions { get; set; }

        public IReadOnlyCollection<string> Permissions { get; set; } = Array.Empty<string>();

        public bool ApplyExcludeFromLeaderboard { get; set; }

        public bool? ExcludeFromLeaderboard { get; set; }

        public bool ApplyBooster { get; set; }

        public decimal? Booster { get; set; }

        public bool ApplyLock { get; set; }

        public bool Unlock { get; set; }

        public int? DurationValue { get; set; }

        public string? DurationUnit { get; set; }

        public bool PermanentLock { get; set; }
    }

    public class BulkUserUpdateResult
    {
        public int TotalSelected { get; set; }

        public int Updated { get; set; }

        public int Skipped { get; set; }

        public List<string> Errors { get; } = new();

        public bool HasErrors => Errors.Count > 0;
    }
}
