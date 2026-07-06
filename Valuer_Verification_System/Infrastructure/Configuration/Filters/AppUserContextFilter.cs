using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Valuer_Verification_System.Services.Permissions;

namespace Valuer_Verification_System.Infrastructure.Configuration.Filters
{
    public sealed class AppUserContextFilter : IAsyncActionFilter
    {
        private readonly IUserService _userService;
        private readonly IPermissionService _permissionService;

        public AppUserContextFilter(
            IUserService userService,
            IPermissionService permissionService)
        {
            _userService = userService;
            _permissionService = permissionService;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (context.Controller is Controller controller)
            {
                var appUser = await _userService.GetCurrentUserAsync();

                if (appUser != null)
                {
                    controller.ViewBag.AppUserFullName = appUser.FullName;
                    controller.ViewBag.AppUserPosition = appUser.Position;
                    controller.ViewBag.AppUserRole = appUser.Role;
                    controller.ViewBag.AppUserInitials = BuildInitials(appUser.FullName);
                    controller.ViewBag.AppUserPages = _permissionService.GetPages(appUser.Role);
                }
            }

            await next();
        }

        private static string BuildInitials(string? fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return "?";

            var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 1)
                return parts[0][0].ToString().ToUpperInvariant();

            return $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
        }
    }
}