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

            if (appUser == null || !permissionService.CanAccess(appUser.Role, _pageKey))
            {
                context.Result = new ForbidResult();
            }
        }
    }
}
