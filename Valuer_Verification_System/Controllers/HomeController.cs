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

            // Role routing
            if (User.IsInRole("Valuer"))
                return RedirectToAction("Dashboard", "Valuer");

            if (User.IsInRole("Manager"))
                return RedirectToAction("Dashboard", "Manager");

            await SetUserContextAsync(ct);
            var vm = await _dash.BuildAsync(ct);
            return View(vm);
        }

        // Shown automatically when RequirePageAccess returns 403
        [HttpGet("/Home/AccessDenied")]
        public async Task<IActionResult> AccessDenied(CancellationToken ct)
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
