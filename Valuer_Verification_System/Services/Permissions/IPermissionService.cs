namespace Valuer_Verification_System.Services.Permissions
{
    public interface IPermissionService
    {
        /// <summary>Returns true if <paramref name="role"/> can access <paramref name="pageKey"/>.</summary>
        bool CanAccess(string? role, string pageKey);

        /// <summary>Returns the full set of page keys accessible to <paramref name="role"/>.</summary>
        IReadOnlySet<string> GetPages(string? role);
    }
}
