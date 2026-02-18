using Valuer_Verification_System.Models.Data;

namespace Valuer_Verification_System.Services.Permissions
{
    public interface IUserService
    {
        /// <summary>
        /// Returns the AppUser for the current Windows-authenticated request,
        /// or null if the user is not found / inactive / has no confirmed system access.
        /// </summary>
        Task<AppUser?> GetCurrentUserAsync(CancellationToken ct = default);

        /// <summary>
        /// Returns the AppUser for an explicit Windows username string.
        /// </summary>
        Task<AppUser?> GetUserAsync(string windowsUsername, CancellationToken ct = default);
    }
}
