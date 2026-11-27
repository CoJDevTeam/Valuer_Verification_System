using Microsoft.AspNetCore.Mvc;
using Valuer_Verification_System.Infrastructure.Configuration.Filters;
using Valuer_Verification_System.Services.Qa;

namespace Valuer_Verification_System.Controllers
{
    public sealed class QaVm
    {
        public List<QaBatch> Batches = new();
        public int BatchId;
        public string Sector = "";
        public string Filter = "review";
        public List<QaSectorRow> Sectors = new();
        public List<QaItem> Items = new();
        public bool Signed; public string? SignedBy; public DateTime? SignedAt;
        public string? Msg; public bool MsgOk;
        public string Who = "";
    }

    public sealed class QaController : Controller
    {
        private readonly IQaService _qa;
        public QaController(IQaService qa) { _qa = qa; }
        private string Me => User.Identity?.Name ?? "unknown";

        private async Task<QaVm> BaseAsync(int? batchId, CancellationToken ct)
        {
            var vm = new QaVm { Who = Me, Batches = await _qa.BatchesAsync(ct) };
            vm.BatchId = batchId ?? vm.Batches.FirstOrDefault()?.Id ?? 0;
            if (vm.BatchId > 0)
            {
                vm.Sectors = await _qa.SectorsAsync(vm.BatchId, ct);
                (vm.Signed, vm.SignedBy, vm.SignedAt) = await _qa.SignoffAsync(vm.BatchId, ct);
            }
            if (TempData["QaMsg"] is string m) { vm.Msg = m; vm.MsgOk = (TempData["QaOk"] as string) == "1"; }
            return vm;
        }
        private IActionResult Back(string action, int batchId, string? sector, string? filter, (bool ok, string msg) res)
        {
            TempData["QaMsg"] = res.msg; TempData["QaOk"] = res.ok ? "1" : "0";
            return RedirectToAction(action, new { batchId, sector, filter });
        }

        // ---------- Area Manager ----------
        [RequirePageAccess("AreaQA")]
        [HttpGet("/Qa/Area")]
        public async Task<IActionResult> Area(int? batchId, string? sector, string? filter, CancellationToken ct)
        {
            var vm = await BaseAsync(batchId, ct);
            vm.Filter = string.IsNullOrWhiteSpace(filter) ? "review" : filter;
            vm.Sector = sector ?? vm.Sectors.FirstOrDefault()?.Sector ?? "";
            if (vm.BatchId > 0 && vm.Sector != "") vm.Items = await _qa.ListAsync(vm.BatchId, vm.Sector, vm.Filter, ct);
            return View(vm);
        }

        [RequirePageAccess("AreaQA")]
        [HttpPost("/Qa/Area/SendBack"), ValidateAntiForgeryToken]
        public async Task<IActionResult> SendBack(int batchId, string sector, string filter, string key, string note, CancellationToken ct)
            => Back("Area", batchId, sector, filter, await _qa.SendBackAsync(batchId, key, note, Me, ct));

        [RequirePageAccess("AreaQA")]
        [HttpPost("/Qa/Area/Approve"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int batchId, string sector, string filter, CancellationToken ct)
            => Back("Area", batchId, sector, filter, await _qa.ApproveSectorAsync(batchId, sector, Me, ct));

        // ---------- Senior Manager ----------
        [RequirePageAccess("SeniorQA")]
        [HttpGet("/Qa/Senior")]
        public async Task<IActionResult> Senior(int? batchId, CancellationToken ct) => View(await BaseAsync(batchId, ct));

        [RequirePageAccess("SeniorQA")]
        [HttpPost("/Qa/Senior/SignOff"), ValidateAntiForgeryToken]
        public async Task<IActionResult> SignOff(int batchId, string? note, CancellationToken ct)
            => Back("Senior", batchId, null, null, await _qa.SignOffAsync(batchId, Me, note, ct));
    }
}
