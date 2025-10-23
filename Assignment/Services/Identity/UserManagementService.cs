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
    public class UserManagementService : IUserManagementService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly ApplicationDbContext _context;

        private static readonly IReadOnlyCollection<string> ManagedPermissionKeys = PermissionRegistry.AllPermissionKeys;

        public UserManagementService(
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            ApplicationDbContext context)
        {
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<PagedResult<UserListItemViewModel>> GetUsersAsync(
            string? keyword,
            string? status,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var query = _userManager.Users.AsNoTracking();
            var now = DateTimeOffset.UtcNow;

            status = status?.Trim().ToLowerInvariant();
            query = status switch
            {
                "locked" => query.Where(user => user.LockoutEnd.HasValue && user.LockoutEnd.Value > now),
                "active" => query.Where(user => !user.LockoutEnd.HasValue || user.LockoutEnd.Value <= now),
                _ => query,
            };

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();
                query = query.Where(user =>
                    (user.Email != null && user.Email.Contains(keyword)) ||
                    (user.UserName != null && user.UserName.Contains(keyword)) ||
                    (user.FullName != null && user.FullName.Contains(keyword)));
            }

            var pagedResult = new PagedResult<UserListItemViewModel>
            {
                CurrentPage = page,
                PageSize = pageSize,
                PageSizeOptions = new[] { 5, 10, 20, 50 },
            };

            pagedResult.TotalItems = await query.CountAsync(cancellationToken);
            pagedResult.EnsureValidPage();

            var skip = (pagedResult.CurrentPage - 1) * pagedResult.PageSize;

            var users = await query
                .OrderBy(user => user.Email)
                .ThenBy(user => user.UserName)
                .Skip(skip)
                .Take(pagedResult.PageSize)
                .Select(user => new
                {
                    user.Id,
                    user.Email,
                    user.UserName,
                    user.FullName,
                    user.LockoutEnd,
                    user.ExcludeFromLeaderboard,
                    user.Booster,
                })
                .ToListAsync(cancellationToken);

            var items = users
                .Select(user => new UserListItemViewModel
                {
                    Id = user.Id,
                    Email = user.Email,
                    UserName = user.UserName,
                    FullName = user.FullName,
                    LockoutEnd = user.LockoutEnd,
                    IsLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd.Value > now,
                    ExcludeFromLeaderboard = user.ExcludeFromLeaderboard,
                    Booster = user.Booster,
                })
                .ToList();

            if (items.Count > 0)
            {
                var userIds = items.Select(item => item.Id).ToList();

                var userRoles = await _context.UserRoles
                    .Where(ur => userIds.Contains(ur.UserId))
                    .ToListAsync(cancellationToken);

                var roleIds = userRoles.Select(ur => ur.RoleId).Distinct().ToList();
                var roles = await _context.Roles
                    .Where(role => roleIds.Contains(role.Id))
                    .Select(role => new { role.Id, role.Name })
                    .ToListAsync(cancellationToken);

                var superAdmins = await _context.UserClaims
                    .Where(claim => userIds.Contains(claim.UserId) && claim.ClaimType == "superadmin")
                    .ToListAsync(cancellationToken);

                foreach (var item in items)
                {
                    var roleNames = userRoles
                        .Where(ur => ur.UserId == item.Id)
                        .Select(ur => roles.FirstOrDefault(role => role.Id == ur.RoleId)?.Name)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Cast<string>()
                        .OrderBy(name => name)
                        .ToList();

                    item.Roles = roleNames;
                    item.IsSuperAdmin = superAdmins.Any(claim =>
                        claim.UserId == item.Id &&
                        string.Equals(claim.ClaimValue, "true", StringComparison.OrdinalIgnoreCase));
                }
            }

            pagedResult.SetItems(items);
            return pagedResult;
        }

        public async Task<UserManagementViewModel?> GetUserAsync(string userId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            var user = await _userManager.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

            if (user == null)
            {
                return null;
            }

            var roles = await _userManager.GetRolesAsync(user);
            var directClaims = await _userManager.GetClaimsAsync(user);
            var roleClaims = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var roleName in roles)
            {
                var role = await _roleManager.FindByNameAsync(roleName);
                if (role == null)
                {
                    continue;
                }

                var claims = await _roleManager.GetClaimsAsync(role);
                foreach (var claim in claims.Where(IsManagedClaim))
                {
                    roleClaims.Add(claim.Type);
                }
            }

            var selectedDirect = directClaims
                .Where(IsManagedClaim)
                .Select(claim => claim.Type)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var permissions = BuildPermissionViewModels(selectedDirect, roleClaims);

            var isSuperAdmin = directClaims.Any(claim =>
                string.Equals(claim.Type, "superadmin", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(claim.Value, "true", StringComparison.OrdinalIgnoreCase));

            return new UserManagementViewModel
            {
                Id = user.Id,
                Email = user.Email,
                UserName = user.UserName,
                FullName = user.FullName,
                LockoutEnd = user.LockoutEnd,
                IsLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow,
                Roles = roles.OrderBy(role => role).ToList(),
                Permissions = permissions,
                IsSuperAdmin = isSuperAdmin,
                ExcludeFromLeaderboard = user.ExcludeFromLeaderboard,
                Booster = user.Booster,
            };
        }

        public async Task<(bool Success, string? ErrorMessage)> UpdateUserRolesAsync(string userId, IEnumerable<string> roles, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return (false, "Người dùng không hợp lệ.");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return (false, "Người dùng không tồn tại.");
            }

            if (await IsSuperAdminAsync(user))
            {
                return (false, "Không thể chỉnh sửa người dùng superadmin.");
            }

            var normalizedRoles = roles?
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Select(role => role.Trim())
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            var normalizedRoleNames = normalizedRoles
                .Select(role => role.ToUpperInvariant())
                .ToList();

            var validRoles = await _roleManager.Roles
                .Where(role => !role.IsDeleted && role.NormalizedName != null && normalizedRoleNames.Contains(role.NormalizedName))
                .Select(role => role.Name)
                .Where(name => name != null)
                .Select(name => name!)
                .ToListAsync(cancellationToken);

            var currentRoles = await _userManager.GetRolesAsync(user);

            var rolesToRemove = currentRoles
                .Where(role => !validRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
                .ToList();

            var rolesToAdd = validRoles
                .Where(role => !currentRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (rolesToRemove.Count > 0)
            {
                var result = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
                if (!result.Succeeded)
                {
                    return (false, string.Join("; ", result.Errors.Select(error => error.Description)));
                }
            }

            if (rolesToAdd.Count > 0)
            {
                var result = await _userManager.AddToRolesAsync(user, rolesToAdd);
                if (!result.Succeeded)
                {
                    return (false, string.Join("; ", result.Errors.Select(error => error.Description)));
                }
            }

            return (true, null);
        }

        public async Task<(bool Success, string? ErrorMessage)> UpdateUserPermissionsAsync(string userId, IEnumerable<string> permissions, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return (false, "Người dùng không hợp lệ.");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return (false, "Người dùng không tồn tại.");
            }

            if (await IsSuperAdminAsync(user))
            {
                return (false, "Không thể chỉnh sửa người dùng superadmin.");
            }

            var normalizedPermissions = permissions?
                .Where(permission => !string.IsNullOrWhiteSpace(permission))
                .Select(permission => permission.Trim())
                .Where(permission => ManagedPermissionKeys.Contains(permission))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
                ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var existingClaims = await _userManager.GetClaimsAsync(user);

            foreach (var claim in existingClaims.Where(IsManagedClaim).ToList())
            {
                await _userManager.RemoveClaimAsync(user, claim);
            }

            foreach (var permission in normalizedPermissions)
            {
                await _userManager.AddClaimAsync(user, new Claim(permission, "true"));
            }

            return (true, null);
        }

        public async Task<(bool Success, string? ErrorMessage)> UpdateUserSettingsAsync(string userId, bool excludeFromLeaderboard, decimal booster, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return (false, "Người dùng không hợp lệ.");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return (false, "Người dùng không tồn tại.");
            }

            if (await IsSuperAdminAsync(user))
            {
                return (false, "Không thể chỉnh sửa người dùng superadmin.");
            }

            user.ExcludeFromLeaderboard = excludeFromLeaderboard;
            user.Booster = NormalizeBooster(booster);

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return (false, string.Join("; ", result.Errors.Select(error => error.Description)));
            }

            return (true, null);
        }

        public Task<IReadOnlyList<PermissionViewModel>> GetPermissionDefinitionsAsync(CancellationToken cancellationToken = default)
        {
            var permissions = BuildPermissionViewModels(
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            return Task.FromResult(permissions);
        }

        public async Task<BulkUserUpdateResult> BulkUpdateUsersAsync(BulkUserUpdateRequest request, CancellationToken cancellationToken = default)
        {
            var result = new BulkUserUpdateResult();

            if (request == null || request.UserIds == null || request.UserIds.Count == 0)
            {
                return result;
            }

            var distinctIds = request.UserIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            result.TotalSelected = distinctIds.Count;
            if (distinctIds.Count == 0)
            {
                return result;
            }

            var users = await _userManager.Users
                .Where(user => distinctIds.Contains(user.Id))
                .ToListAsync(cancellationToken);

            foreach (var user in users)
            {
                if (await IsSuperAdminAsync(user))
                {
                    result.Skipped++;
                    result.Errors.Add($"Không thể chỉnh sửa superadmin {user.Email ?? user.UserName ?? user.Id}.");
                    continue;
                }

                var userUpdated = false;
                var errors = new List<string>();

                if (request.ApplyRoles)
                {
                    var (success, errorMessage) = await UpdateUserRolesAsync(user.Id, request.Roles, cancellationToken);
                    if (!success && !string.IsNullOrWhiteSpace(errorMessage))
                    {
                        errors.Add(errorMessage);
                    }
                    else if (success)
                    {
                        userUpdated = true;
                    }
                }

                if (request.ApplyPermissions)
                {
                    var (success, errorMessage) = await UpdateUserPermissionsAsync(user.Id, request.Permissions, cancellationToken);
                    if (!success && !string.IsNullOrWhiteSpace(errorMessage))
                    {
                        errors.Add(errorMessage);
                    }
                    else if (success)
                    {
                        userUpdated = true;
                    }
                }

                var shouldUpdateUser = false;
                if (request.ApplyExcludeFromLeaderboard)
                {
                    user.ExcludeFromLeaderboard = request.ExcludeFromLeaderboard ?? false;
                    shouldUpdateUser = true;
                }

                if (request.ApplyBooster)
                {
                    user.Booster = NormalizeBooster(request.Booster);
                    shouldUpdateUser = true;
                }

                if (shouldUpdateUser)
                {
                    var updateResult = await _userManager.UpdateAsync(user);
                    if (!updateResult.Succeeded)
                    {
                        errors.Add(string.Join("; ", updateResult.Errors.Select(error => error.Description)));
                    }
                    else
                    {
                        userUpdated = true;
                    }
                }

                if (request.ApplyLock)
                {
                    var isPermanentLock = request.PermanentLock;
                    if (!request.Unlock && !isPermanentLock && (!request.DurationValue.HasValue || request.DurationValue.Value <= 0 || string.IsNullOrWhiteSpace(request.DurationUnit)))
                    {
                        errors.Add("Thông tin khóa tài khoản không hợp lệ.");
                    }
                    else
                    {
                        var lockModel = new LockUserViewModel
                        {
                            UserId = user.Id,
                            Unlock = request.Unlock,
                            DurationValue = request.Unlock || isPermanentLock ? 0 : request.DurationValue ?? 0,
                            DurationUnit = isPermanentLock ? "permanent" : request.DurationUnit ?? "minute",
                            IsPermanent = isPermanentLock,
                        };

                        var (success, errorMessage) = await UpdateUserLockoutAsync(lockModel, cancellationToken);
                        if (!success && !string.IsNullOrWhiteSpace(errorMessage))
                        {
                            errors.Add(errorMessage);
                        }
                        else if (success)
                        {
                            userUpdated = true;
                        }
                    }
                }

                if (errors.Count > 0)
                {
                    var label = user.Email ?? user.UserName ?? user.Id;
                    result.Errors.Add($"{label}: {string.Join("; ", errors)}");
                }

                if (userUpdated)
                {
                    result.Updated++;
                }
            }

            var processedIds = users.Select(u => u.Id).ToHashSet(StringComparer.Ordinal);
            var missing = distinctIds.Where(id => !processedIds.Contains(id)).ToList();
            if (missing.Count > 0)
            {
                result.Errors.Add($"Không tìm thấy {missing.Count} người dùng.");
            }

            result.Skipped += missing.Count;

            return result;
        }

        public async Task<(bool Success, string? ErrorMessage)> UpdateUserLockoutAsync(LockUserViewModel model, CancellationToken cancellationToken = default)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.UserId))
            {
                return (false, "Dữ liệu không hợp lệ.");
            }

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                return (false, "Người dùng không tồn tại.");
            }

            if (await IsSuperAdminAsync(user))
            {
                return (false, "Không thể chỉnh sửa người dùng superadmin.");
            }

            if (model.Unlock)
            {
                user.LockoutEnd = null;
                user.LockoutEnabled = false;
                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    return (false, string.Join("; ", result.Errors.Select(error => error.Description)));
                }

                return (true, null);
            }

            if (model.IsPermanent)
            {
                user.LockoutEnabled = true;
                user.LockoutEnd = DateTimeOffset.MaxValue;

                var updatePermanentResult = await _userManager.UpdateAsync(user);
                if (!updatePermanentResult.Succeeded)
                {
                    return (false, string.Join("; ", updatePermanentResult.Errors.Select(error => error.Description)));
                }

                return (true, null);
            }

            if (model.DurationValue <= 0)
            {
                return (false, "Thời gian khóa phải lớn hơn 0.");
            }

            var lockoutDuration = GetLockoutDuration(model.DurationValue, model.DurationUnit);
            if (lockoutDuration <= TimeSpan.Zero)
            {
                return (false, "Đơn vị thời gian không hợp lệ.");
            }

            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.UtcNow.Add(lockoutDuration);

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                return (false, string.Join("; ", updateResult.Errors.Select(error => error.Description)));
            }

            return (true, null);
        }

        public async Task<IReadOnlyList<RoleListItemViewModel>> GetAssignableRolesAsync(CancellationToken cancellationToken = default)
        {
            return await _roleManager.Roles
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
        }

        private async Task<bool> IsSuperAdminAsync(ApplicationUser user)
        {
            var claims = await _userManager.GetClaimsAsync(user);
            return claims.Any(claim =>
                string.Equals(claim.Type, "superadmin", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(claim.Value, "true", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsManagedClaim(Claim claim)
        {
            return ManagedPermissionKeys.Contains(claim.Type) &&
                   string.Equals(claim.Value, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static decimal NormalizeBooster(decimal? value)
        {
            if (!value.HasValue || value.Value <= 0)
            {
                return 1m;
            }

            var rounded = Math.Round(value.Value, 2, MidpointRounding.AwayFromZero);
            return rounded < 1m ? 1m : rounded;
        }

        private static IReadOnlyList<PermissionViewModel> BuildPermissionViewModels(
            IReadOnlySet<string> directPermissions,
            IReadOnlySet<string> inheritedPermissions)
        {
            var result = new List<PermissionViewModel>();

            foreach (var group in PermissionRegistry.Groups)
            {
                foreach (var permission in group.Permissions)
                {
                    var key = permission.Key;
                    var isDirect = directPermissions.Contains(key);
                    var isInherited = inheritedPermissions.Contains(key);

                    result.Add(new PermissionViewModel
                    {
                        Group = group.Name,
                        Key = key,
                        DisplayName = permission.DisplayName,
                        IsSelected = isDirect || isInherited,
                        IsInherited = !isDirect && isInherited,
                    });
                }
            }

            return result;
        }

        private static TimeSpan GetLockoutDuration(int value, string? unit)
        {
            unit = unit?.Trim().ToLowerInvariant();
            return unit switch
            {
                "minute" or "minutes" => TimeSpan.FromMinutes(value),
                "hour" or "hours" => TimeSpan.FromHours(value),
                "day" or "days" => TimeSpan.FromDays(value),
                _ => TimeSpan.Zero,
            };
        }
    }
}
