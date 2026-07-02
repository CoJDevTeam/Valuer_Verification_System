using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Valuer_Verification_System.Infrastructure.Configuration.Filters;
using Valuer_Verification_System.Models;
using Valuer_Verification_System.Services.Home;
using Valuer_Verification_System.Services.Permissions;


namespace Valuer_Verification_System.Controllers
{
    public class HomeController : AppBaseController
    {
       
            private readonly IDataTeamDashboardService _dash;

            public HomeController(IDataTeamDashboardService dash, IUserService userSvc, IPermissionService permSvc)
             : base(userSvc, permSvc)
        {
                _dash = dash;
            }

        [RequirePageAccess("Home")]
        [HttpGet("/")]
        [HttpGet("/Home")]
        [HttpGet("/Home/Index")]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            await SetUserContextAsync(ct);

            var position = GetUserPositionOrRole();

            // Role / position routing without User.IsInRole()
            if (HasAppRole("Valuer"))
                return RedirectToAction("Dashboard", "Valuer");

            if (HasAppRole("Manager", "Sector Manager"))
                return RedirectToAction("Dashboard", "Manager");

            var vm = await _dash.BuildAsync(ct);
            return View(vm);
        }


        private string GetUserPositionOrRole()
        {
            return User.FindFirst("Position")?.Value
                ?? User.FindFirst("Role")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                ?? "";
        }

        private bool HasAppRole(params string[] allowedRoles)
        {
            var role = GetUserPositionOrRole();

            if (string.IsNullOrWhiteSpace(role))
                return false;

            return allowedRoles.Any(x =>
                role.Equals(x, StringComparison.OrdinalIgnoreCase) ||
                role.Contains(x, StringComparison.OrdinalIgnoreCase));
        }
        // Shown automatically when RequirePageAccess returns 403
        [HttpGet("/Home/AccessDenied")]
        public async Task<IActionResult> AccessDenied(CancellationToken ct)
        {
            await SetUserContextAsync(ct);
            return View();
        }


        [HttpGet]
        public async Task<IActionResult> Privacy(CancellationToken ct)
        {
            await SetUserContextAsync(ct);
            return View();
        }
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
