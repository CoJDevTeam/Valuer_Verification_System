using Microsoft.AspNetCore.Mvc;
using Valuer_Verification_System.Domain.WeekWindows;
using Valuer_Verification_System.Models.ViewModels;

namespace Valuer_Verification_System.Controllers
{
    public sealed class WeekWindowsController : Controller
    {
        private readonly IWeekWindowService _svc;

        public WeekWindowsController(IWeekWindowService svc)
        {
            _svc = svc;
        }

        [HttpGet("/WeekWindows")]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var list = await _svc.ListAsync(200, ct);
            return View(list);
        }

        [HttpGet("/WeekWindows/Create")]
        public IActionResult Create()
            => View(new WeekWindowCreateVm());

        [HttpPost("/WeekWindows/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(WeekWindowCreateVm vm, CancellationToken ct)
        {
            var user = User?.Identity?.Name ?? "system";
            var ww = await _svc.CreateOrGetAsync(vm.SelectedDate, user, ct);
            return RedirectToAction(nameof(Details), new { id = ww.Id });
        }

        [HttpGet("/WeekWindows/Details/{id:int}")]
        public async Task<IActionResult> Details(int id, CancellationToken ct)
        {
            var ww = await _svc.GetAsync(id, ct);
            if (ww is null) return NotFound();

            var vm = new WeekWindowDetailsVm
            {
                Id = ww.Id,
                WeekStart = ww.WeekStart,
                WeekEnd = ww.WeekEnd,
                NoticeSendDate = ww.NoticeSendDate,
                Status = ww.Status.ToString()
            };

            return View(vm);
        }
    }

}
