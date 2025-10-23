using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Assignment.Authorization;
using Assignment.Data;
using Assignment.Models;
using Assignment.ViewModels;
using Assignment.ViewModels.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Assignment.Services.Identity
{
    public class RoleManagementService : IRoleManagementService
    {
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly ApplicationDbContext _context;

        private static readonly IReadOnlyCollection<string> ManagedPermissionKeys = PermissionRegistry.AllPermissionKeys;

        public RoleManagementService(
            RoleManager<ApplicationRole> roleManager,
            ApplicationDbContext context)
        {
            _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<PagedResult<RoleListItemViewModel>> GetRolesAsync(
            string? keyword,
            string? status,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var query = _roleManager.Roles.AsNoTracking();

            status = status?.Trim().ToLowerInvariant();

            query = status switch
            {
                "deleted" => query.Where(role => role.IsDeleted),
                "all" => query,
                "active" => query.Where(role => !role.IsDeleted),
                _ => query.Where(role => !role.IsDeleted),
            };

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();
                query = query.Where(role => role.Name != null && role.Name.Contains(keyword));
            }

            var pagedResult = new PagedResult<RoleListItemViewModel>
            {
                CurrentPage = page,
                PageSize = pageSize,
                PageSizeOptions = new[] { 5, 10, 20, 50 },
            };

            pagedResult.TotalItems = await query.CountAsync(cancellationToken);
            pagedResult.EnsureValidPage();

            var skip = (pagedResult.CurrentPage - 1) * pagedResult.PageSize;

            var items = await query
                .OrderBy(role => role.Name)
                .Skip(skip)
                .Take(pagedResult.PageSize)
                .Select(role => new RoleListItemViewModel
                {
                    Id = role.Id,
                    Name = role.Name ?? string.Empty,
                    IsDeleted = role.IsDeleted,
                    CreatedAt = role.CreatedAt,
                    UpdatedAt = role.UpdatedAt,
                })
                .ToListAsync(cancellationToken);

            if (items.Count > 0)
            {
                var roleIds = items.Select(item => item.Id).ToList();
                var assignments = await _context.UserRoles
                    .Where(userRole => roleIds.Contains(userRole.RoleId))
                    .GroupBy(userRole => userRole.RoleId)
                    .Select(group => new
                    {
                        group.Key,
                        Count = group.Count(),
                    })
                    .ToListAsync(cancellationToken);

                foreach (var item in items)
                {
                    var assignment = assignments.FirstOrDefault(a => a.Key == item.Id);
                    if (assignment != null)
                    {
                        item.AssignedUserCount = assignment.Count;
                    }
                }
            }

            pagedResult.SetItems(items);
            return pagedResult;
        }

        public Task<RoleManagementViewModel> CreateTemplateAsync(CancellationToken cancellationToken = default)
        {
            var viewModel = new RoleManagementViewModel
            {
                CreatedAt = DateTime.UtcNow,
                Permissions = BuildPermissionViewModels(),
            };

            return Task.FromResult(viewModel);
        }

        public async Task<RoleManagementViewModel?> GetRoleAsync(string roleId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(roleId))
            {
                return null;
            }

            var role = await _roleManager.Roles
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken);

            if (role == null)
            {
                return null;
            }

            var assignedUserCount = await _context.UserRoles
                .CountAsync(ur => ur.RoleId == roleId, cancellationToken);

            var claims = await _roleManager.GetClaimsAsync(role);
            var selectedPermissions = claims
                .Where(claim => IsTrueClaim(claim) && ManagedPermissionKeys.Contains(claim.Type))
                .Select(claim => claim.Type)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return new RoleManagementViewModel
            {
                Id = role.Id,
                Name = role.Name ?? string.Empty,
                IsDeleted = role.IsDeleted,
                CreatedAt = role.CreatedAt,
                CreatedBy = role.CreatedBy,
                UpdatedAt = role.UpdatedAt,
                UpdatedBy = role.UpdatedBy,
                AssignedUserCount = assignedUserCount,
                Permissions = BuildPermissionViewModels(selectedPermissions),
            };
        }

        public async Task<IdentityResult> CreateRoleAsync(RoleManagementViewModel model, string currentUserId, CancellationToken cancellationToken = default)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            var role = new ApplicationRole
            {
                Name = model.Name?.Trim() ?? string.Empty,
                NormalizedName = model.Name?.Trim().ToUpperInvariant(),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = currentUserId,
                IsDeleted = false,
            };

            var createResult = await _roleManager.CreateAsync(role);
            if (!createResult.Succeeded)
            {
                return createResult;
            }

            await UpdateRoleClaimsAsync(role, model.Permissions);

            role = await _roleManager.FindByIdAsync(role.Id) ?? role;

            role.UpdatedAt = DateTime.UtcNow;
            role.UpdatedBy = currentUserId;

            await _roleManager.UpdateAsync(role);

            return IdentityResult.Success;
        }

        public async Task<IdentityResult> UpdateRoleAsync(RoleManagementViewModel model, string currentUserId, CancellationToken cancellationToken = default)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (string.IsNullOrWhiteSpace(model.Id))
            {
                return IdentityResult.Failed(new IdentityError
                {
                    Description = "Role identifier is required.",
                });
            }

            var role = await _roleManager.FindByIdAsync(model.Id);
            if (role == null)
            {
                return IdentityResult.Failed(new IdentityError
                {
                    Description = "Role not found.",
                });
            }

            role.Name = model.Name?.Trim() ?? role.Name;
            role.NormalizedName = role.Name?.ToUpperInvariant();
            role.UpdatedAt = DateTime.UtcNow;
            role.UpdatedBy = currentUserId;

            var updateResult = await _roleManager.UpdateAsync(role);
            if (!updateResult.Succeeded)
            {
                return updateResult;
            }

            await UpdateRoleClaimsAsync(role, model.Permissions);

            return IdentityResult.Success;
        }

        public async Task<(bool Success, string? ErrorMessage)> SoftDeleteRoleAsync(string roleId, string currentUserId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(roleId))
            {
                return (false, "Role identifier is required.");
            }

            var role = await _roleManager.FindByIdAsync(roleId);
            if (role == null)
            {
                return (false, "Role không tồn tại.");
            }

            var hasAssignments = await _context.UserRoles.AnyAsync(ur => ur.RoleId == roleId, cancellationToken);
            if (hasAssignments)
            {
                return (false, "Không thể xóa vai trò đang được gán cho người dùng.");
            }

            role.IsDeleted = true;
            role.UpdatedAt = DateTime.UtcNow;
            role.UpdatedBy = currentUserId;

            var result = await _roleManager.UpdateAsync(role);
            if (!result.Succeeded)
            {
                return (false, string.Join("; ", result.Errors.Select(e => e.Description)));
            }

            return (true, null);
        }

        public async Task<IReadOnlyList<RoleListItemViewModel>> GetActiveRolesAsync(CancellationToken cancellationToken = default)
        {
            var roles = await _roleManager.Roles
                .AsNoTracking()
                .Where(role => !role.IsDeleted)
                .OrderBy(role => role.Name)
                .Select(role => new RoleListItemViewModel
                {
                    Id = role.Id,
                    Name = role.Name ?? string.Empty,
                    IsDeleted = role.IsDeleted,
                    CreatedAt = role.CreatedAt,
                    UpdatedAt = role.UpdatedAt,
                })
                .ToListAsync(cancellationToken);

            if (roles.Count > 0)
            {
                var roleIds = roles.Select(role => role.Id).ToList();
                var counts = await _context.UserRoles
                    .Where(userRole => roleIds.Contains(userRole.RoleId))
                    .GroupBy(userRole => userRole.RoleId)
                    .Select(group => new
                    {
                        group.Key,
                        Count = group.Count(),
                    })
                    .ToListAsync(cancellationToken);

                foreach (var role in roles)
                {
                    var count = counts.FirstOrDefault(c => c.Key == role.Id);
                    if (count != null)
                    {
                        role.AssignedUserCount = count.Count;
                    }
                }
            }

            return roles;
        }

        private static bool IsTrueClaim(Claim claim)
        {
            return string.Equals(claim.Value, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static IReadOnlyList<PermissionViewModel> BuildPermissionViewModels(IEnumerable<string>? selectedPermissions = null)
        {
            var selectedSet = new HashSet<string>(selectedPermissions ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);

            var result = new List<PermissionViewModel>();
            foreach (var group in PermissionRegistry.Groups)
            {
                foreach (var permission in group.Permissions)
                {
                    result.Add(new PermissionViewModel
                    {
                        Group = group.Name,
                        Key = permission.Key,
                        DisplayName = permission.DisplayName,
                        IsSelected = selectedSet.Contains(permission.Key),
                    });
                }
            }

            return result;
        }

        private async Task UpdateRoleClaimsAsync(ApplicationRole role, IReadOnlyList<PermissionViewModel> permissions)
        {
            var effectivePermissions = permissions?
                .Where(permission => permission.IsSelected)
                .Select(permission => permission.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
                ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var existingClaims = await _roleManager.GetClaimsAsync(role);

            foreach (var claim in existingClaims.Where(claim => ManagedPermissionKeys.Contains(claim.Type)))
            {
                if (!effectivePermissions.Contains(claim.Type) || !IsTrueClaim(claim))
                {
                    await _roleManager.RemoveClaimAsync(role, claim);
                }
            }

            foreach (var permission in effectivePermissions)
            {
                if (!existingClaims.Any(claim => ManagedPermissionKeys.Contains(claim.Type) && string.Equals(claim.Type, permission, StringComparison.OrdinalIgnoreCase)))
                {
                    await _roleManager.AddClaimAsync(role, new Claim(permission, "true"));
                }
            }
        }
    }
}
