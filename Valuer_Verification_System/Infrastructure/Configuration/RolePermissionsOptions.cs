namespace Valuer_Verification_System.Infrastructure.Configuration
{
    public class RolePermissionsOptions
    {
        public const string SectionName = "RolePermissions";

        // Dictionary key = role name (must match RoleName in [UserManagement].[dbo].[Roles])
        // Dictionary value = the settings for that role
        public Dictionary<string, RolePageAccess> Roles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The page-access settings for a single role.
    /// </summary>
    public class RolePageAccess
    {
        /// <summary>
        /// List of page keys this role can access.
        /// Recognised page keys (case-insensitive):
        ///   Home | WeekWindows | ValuerInbox | ValuerDashboard | ManagerDashboard | Reassign
        /// </summary>
        public List<string> Pages { get; set; } = new();
    }
}
