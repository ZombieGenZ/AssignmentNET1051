using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Assignment.ViewModels;
using Assignment.ViewModels.Identity;
using Microsoft.AspNetCore.Identity;

namespace Assignment.Services.Identity
{
    public interface IRoleManagementService
    {
        Task<PagedResult<RoleListItemViewModel>> GetRolesAsync(
            string? keyword,
            string? status,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);

        Task<RoleManagementViewModel> CreateTemplateAsync(CancellationToken cancellationToken = default);

        Task<RoleManagementViewModel?> GetRoleAsync(string roleId, CancellationToken cancellationToken = default);

        Task<IdentityResult> CreateRoleAsync(RoleManagementViewModel model, string currentUserId, CancellationToken cancellationToken = default);

        Task<IdentityResult> UpdateRoleAsync(RoleManagementViewModel model, string currentUserId, CancellationToken cancellationToken = default);

        Task<(bool Success, string? ErrorMessage)> SoftDeleteRoleAsync(string roleId, string currentUserId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<RoleListItemViewModel>> GetActiveRolesAsync(CancellationToken cancellationToken = default);
    }
}
