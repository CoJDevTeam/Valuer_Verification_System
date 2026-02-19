using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Valuer_Verification_System.Services.Permissions;

namespace Valuer_Verification_System.Infrastructure.Configuration.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class RequirePageAccessAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly string _pageKey;

        public RequirePageAccessAttribute(string pageKey)
        {
            _pageKey = pageKey;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var userService = context.HttpContext.RequestServices.GetRequiredService<IUserService>();
            var permissionService = context.HttpContext.RequestServices.GetRequiredService<IPermissionService>();

            var appUser = await userService.GetCurrentUserAsync();

            if (appUser == null || !appUser.Active)
            {
                context.Result = new ForbidResult();
                return;
            }

            if (permissionService.CanAccess(appUser.Role, _pageKey))
                return;

            // ✅ Redirect ONLY when denying Home
            var isHome = _pageKey.Equals("Home", StringComparison.OrdinalIgnoreCase);
            if (!isHome)
            {
                context.Result = new ForbidResult();
                return;
            }

            var role = (appUser.Role ?? "").Trim();

            if (role.Equals("Valuer", StringComparison.OrdinalIgnoreCase))
            {
                context.Result = new RedirectToRouteResult(new RouteValueDictionary(new
                {
                    controller = "Valuer",
                    action = "Dashboard"
                }));
                return;
            }

            if (role.Equals("Manager", StringComparison.OrdinalIgnoreCase))
            {
                context.Result = new RedirectToRouteResult(new RouteValueDictionary(new
                {
                    controller = "Manager",
                    action = "Dashboard"
                }));
                return;
            }

            // Data Team / others: forbid (or allow if you want)
            context.Result = new ForbidResult();
        }

    }
}
