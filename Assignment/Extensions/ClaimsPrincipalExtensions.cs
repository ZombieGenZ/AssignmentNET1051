using System;
using System.Linq;
using System.Security.Claims;

namespace Assignment.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        private static bool HasTrueValue(Claim claim)
            => string.Equals(claim.Value, "true", StringComparison.OrdinalIgnoreCase);

        public static bool HasPermission(this ClaimsPrincipal? user, string permission)
        {
            if (user == null || string.IsNullOrWhiteSpace(permission))
            {
                return false;
            }

            return user.HasClaim(c => string.Equals(c.Type, permission, StringComparison.OrdinalIgnoreCase) && HasTrueValue(c));
        }

        public static bool HasAnyPermission(this ClaimsPrincipal? user, params string[] permissions)
        {
            if (user == null || permissions == null || permissions.Length == 0)
            {
                return false;
            }

            return permissions.Any(permission => user.HasPermission(permission));
        }

        public static bool HasPermissionWithPrefix(this ClaimsPrincipal? user, string prefix)
        {
            if (user == null || string.IsNullOrWhiteSpace(prefix))
            {
                return false;
            }

            return user.Claims.Any(c => c.Type.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && HasTrueValue(c));
        }
    }
}
