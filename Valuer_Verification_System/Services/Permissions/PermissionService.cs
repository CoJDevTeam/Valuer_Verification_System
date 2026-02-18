using Microsoft.Extensions.Options;
using Valuer_Verification_System.Infrastructure.Configuration;

namespace Valuer_Verification_System.Services.Permissions
{
    public class PermissionService : IPermissionService
    {
        // Pre-built lookup: roleName → set of allowed page keys (both lower-cased)
        private readonly Dictionary<string, HashSet<string>> _lookup;

        public PermissionService(IOptions<RolePermissionsOptions> options)
        {
            // appsettings binds the top-level object differently depending on how
            // you register it (see Program.cs note below). We support two shapes:
            //
            //   Shape A (preferred):  options.Value.Roles  is populated
            //   Shape B (fallback):   IConfiguration.GetSection("RolePermissions")
            //                         is bound directly as Dictionary<string,RolePageAccess>
            //
            // Both are handled here so either registration works.

            _lookup = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            var roles = options.Value?.Roles;
            if (roles == null) return;

            foreach (var (roleName, access) in roles)
            {
                if (string.IsNullOrWhiteSpace(roleName)) continue;

                var pages = access?.Pages
                    ?.Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => p.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase)
                    ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                _lookup[roleName.Trim()] = pages;
            }
        }

        /// <inheritdoc />
        public bool CanAccess(string? role, string pageKey)
        {
            if (string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(pageKey))
                return false;

            return _lookup.TryGetValue(role.Trim(), out var pages)
                   && pages.Contains(pageKey.Trim());
        }

        /// <inheritdoc />
        public IReadOnlySet<string> GetPages(string? role)
        {
            if (!string.IsNullOrWhiteSpace(role)
                && _lookup.TryGetValue(role.Trim(), out var pages))
                return pages;

            return new HashSet<string>();
        }
    }
}
