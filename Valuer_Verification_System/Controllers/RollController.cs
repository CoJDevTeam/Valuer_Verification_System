using Microsoft.AspNetCore.Mvc;
using Valuer_Verification_System.Infrastructure.Configuration.Filters;
using Valuer_Verification_System.Services.Roll;

namespace Valuer_Verification_System.Controllers
{
    /// <summary>
    /// Valuation Roll preparation.
    ///   Data Team (page key RollPreparation)      : create from a verified batch, add related reviews / amendments, submit.
    ///   Valuation Department (page key RollConfirmation) : include / exclude each entry and confirm the roll.
    /// </summary>
    public sealed class RollController : Controller
    {
        private readonly IRollPreparationService _svc;

        public RollController(IRollPreparationService svc) => _svc = svc;

        private string Me => User.Identity?.Name ?? "unknown";

        // ───────────── Data Team ─────────────
        [RequirePageAccess("RollPreparation")]
        [HttpGet("/Roll")]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            ViewBag.Mode = "prepare";
            ViewBag.Batches = await _svc.BatchesAsync(ct);
            return View(await _svc.ListAsync(ct));
        }

        [RequirePageAccess("RollPreparation")]
        [ValidateAntiForgeryToken]
        [HttpPost("/Roll/Create")]
        public async Task<IActionResult> Create(RollCreateInput input, CancellationToken ct)
        {
            try
            {
                var id = await _svc.CreateAsync(input, Me, ct);
                TempData["Success"] = "Roll created from the properties marked Yes. Check for related supplementary reviews and amendments, then submit it for confirmation.";
                return Redirect($"/Roll/{id}");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return Redirect("/Roll");
            }
        }

        [RequirePageAccess("RollPreparation")]
        [HttpGet("/Roll/{id:int}")]
        public async Task<IActionResult> Details(int id, string? propertyType, string? source, string? decision, CancellationToken ct)
        {
            var d = await _svc.GetAsync(id, propertyType, source, decision, ct);
            if (d == null) return NotFound();
            ViewBag.Mode = "prepare";
            ViewBag.PropertyType = propertyType; ViewBag.Source = source; ViewBag.Decision = decision;
            return View(d);
        }

        [RequirePageAccess("RollPreparation")]
        [ValidateAntiForgeryToken]
        [HttpPost("/Roll/{id:int}/AddRelated")]
        public async Task<IActionResult> AddRelated(int id, RollRelatedInput input, CancellationToken ct)
        {
            try
            {
                await _svc.AddRelatedAsync(id, input, Me, ct);
                TempData["Success"] = "Related entry added.";
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return Redirect($"/Roll/{id}");
        }

        [RequirePageAccess("RollPreparation")]
        [ValidateAntiForgeryToken]
        [HttpPost("/Roll/{id:int}/RemoveRelated")]
        public async Task<IActionResult> RemoveRelated(int id, int entryId, CancellationToken ct)
        {
            try { await _svc.RemoveRelatedAsync(id, entryId, ct); TempData["Success"] = "Entry removed."; }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return Redirect($"/Roll/{id}");
        }

        [RequirePageAccess("RollPreparation")]
        [ValidateAntiForgeryToken]
        [HttpPost("/Roll/{id:int}/Submit")]
        public async Task<IActionResult> Submit(int id, CancellationToken ct)
        {
            try
            {
                await _svc.SubmitAsync(id, Me, ct);
                TempData["Success"] = "Submitted to the Valuation Department for confirmation.";
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return Redirect($"/Roll/{id}");
        }

        // Confirmation list download: both teams may use it
        [HttpGet("/Roll/{id:int}/Export")]
        [RequirePageAccess("RollPreparation")]
        public Task<IActionResult> Export(int id, CancellationToken ct) => ExportCore(id, ct);

        [HttpGet("/Roll/Confirm/{id:int}/Export")]
        [RequirePageAccess("RollConfirmation")]
        public Task<IActionResult> ExportConfirm(int id, CancellationToken ct) => ExportCore(id, ct);

        private async Task<IActionResult> ExportCore(int id, CancellationToken ct)
        {
            var bytes = await _svc.ExportCsvAsync(id, ct);
            return File(bytes, "text/csv", $"Roll_{id}_ConfirmationList.csv");
        }

        // ───────────── Valuation Department ─────────────
        [RequirePageAccess("RollConfirmation")]
        [HttpGet("/Roll/Confirm")]
        public async Task<IActionResult> ConfirmList(CancellationToken ct)
        {
            ViewBag.Mode = "confirm";
            var all = await _svc.ListAsync(ct);
            return View("Index", all.Where(r => r.Status != "Draft").ToList());
        }

        [RequirePageAccess("RollConfirmation")]
        [HttpGet("/Roll/Confirm/{id:int}")]
        public async Task<IActionResult> Confirm(int id, string? propertyType, string? source, string? decision, CancellationToken ct)
        {
            var d = await _svc.GetAsync(id, propertyType, source, decision, ct);
            if (d == null || d.Header.Status == "Draft") return NotFound();
            ViewBag.Mode = "confirm";
            ViewBag.PropertyType = propertyType; ViewBag.Source = source; ViewBag.Decision = decision;
            return View("Details", d);
        }

        [RequirePageAccess("RollConfirmation")]
        [ValidateAntiForgeryToken]
        [HttpPost("/Roll/Confirm/{id:int}/Decide")]
        public async Task<IActionResult> Decide(int id, int[] entryIds, string decision, string? comment, CancellationToken ct)
        {
            try
            {
                var n = await _svc.DecideAsync(id, entryIds ?? Array.Empty<int>(), decision, comment, Me, ct);
                TempData["Success"] = $"{n} entr{(n == 1 ? "y" : "ies")} set to {decision}.";
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return Redirect($"/Roll/Confirm/{id}");
        }

        [RequirePageAccess("RollConfirmation")]
        [ValidateAntiForgeryToken]
        [HttpPost("/Roll/Confirm/{id:int}/Finalise")]
        public async Task<IActionResult> Finalise(int id, CancellationToken ct)
        {
            try
            {
                await _svc.ConfirmAsync(id, Me, ct);
                TempData["Success"] = "Roll confirmed. The Data Team can now compile the Valuation Roll.";
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return Redirect($"/Roll/Confirm/{id}");
        }
    }
}
