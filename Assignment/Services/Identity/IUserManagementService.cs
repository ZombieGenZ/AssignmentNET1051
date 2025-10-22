using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Assignment.ViewModels;
using Assignment.ViewModels.Identity;
using Microsoft.AspNetCore.Identity;

namespace Assignment.Services.Identity
{
    public interface IUserManagementService
    {
        Task<PagedResult<UserListItemViewModel>> GetUsersAsync(
            string? keyword,
            string? status,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);

        Task<UserManagementViewModel?> GetUserAsync(string userId, CancellationToken cancellationToken = default);

        Task<(bool Success, string? ErrorMessage)> UpdateUserRolesAsync(string userId, IEnumerable<string> roles, CancellationToken cancellationToken = default);

        Task<(bool Success, string? ErrorMessage)> UpdateUserPermissionsAsync(string userId, IEnumerable<string> permissions, CancellationToken cancellationToken = default);

        Task<(bool Success, string? ErrorMessage)> UpdateUserLockoutAsync(LockUserViewModel model, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<RoleListItemViewModel>> GetAssignableRolesAsync(CancellationToken cancellationToken = default);

        Task<(bool Success, string? ErrorMessage)> UpdateUserSettingsAsync(string userId, bool excludeFromLeaderboard, decimal booster, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PermissionViewModel>> GetPermissionDefinitionsAsync(CancellationToken cancellationToken = default);

        Task<BulkUserUpdateResult> BulkUpdateUsersAsync(BulkUserUpdateRequest request, CancellationToken cancellationToken = default);
    }
}
