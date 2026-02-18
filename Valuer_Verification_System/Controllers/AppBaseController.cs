using Microsoft.AspNetCore.Mvc;
using Valuer_Verification_System.Services.Permissions;

namespace Valuer_Verification_System.Controllers
{

        public abstract class AppBaseController : Controller
        {
            protected readonly IUserService UserSvc;
            protected readonly IPermissionService PermSvc;

            protected AppBaseController(IUserService userSvc, IPermissionService permSvc)
            {
                UserSvc = userSvc;
                PermSvc = permSvc;
            }

            /// <summary>
            /// Resolves the current user and writes AppUserFullName, AppUserRole,
            /// AppUserInitials, and AppUserPages into ViewBag for the layout to consume.
            /// </summary>
            protected async Task SetUserContextAsync(CancellationToken ct = default)
            {
                var user = await UserSvc.GetCurrentUserAsync(ct);

                var fullName = user?.FullName ?? User.Identity?.Name ?? "Unknown";
                var role = user?.Role ?? string.Empty;

                ViewBag.AppUserFullName = fullName;
                ViewBag.AppUserRole = role;
                ViewBag.AppUserInitials = BuildInitials(fullName);

                // Pass the full set of allowed page keys so the layout never hard-codes role names
                ViewBag.AppUserPages = PermSvc.GetPages(role);
            }

            private static string BuildInitials(string name)
            {
                if (string.IsNullOrWhiteSpace(name)) return "?";
                var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return parts.Length >= 2
                    ? $"{parts[0][0]}{parts[^1][0]}".ToUpper()
                    : name.Substring(0, Math.Min(2, name.Length)).ToUpper();
            }
        }
    }

