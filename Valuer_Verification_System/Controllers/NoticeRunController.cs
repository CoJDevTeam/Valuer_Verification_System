using Microsoft.AspNetCore.Mvc;
using Valuer_Verification_System.Infrastructure.Configuration.Filters;
using Valuer_Verification_System.Services.Notice;

namespace Valuer_Verification_System.Controllers
{
    public sealed class NoticeRunVm
    {
        public List<(int Id, string Name)> Batches = new();
        public NoticeGate? Gate;
        public int BatchId;
        public string ReviewBaseUrl = "";
        public string? Msg; public bool MsgOk;
    }

    public sealed class NoticeRunController : Controller
    {
        private readonly INoticeRunService _svc;
        public NoticeRunController(INoticeRunService svc) { _svc = svc; }

        [RequirePageAccess("SendNotices")]
        [HttpGet("/Notices/Send")]
        public async Task<IActionResult> Index(int? batchId, CancellationToken ct)
        {
            var vm = new NoticeRunVm { Batches = await _svc.BatchesAsync(ct), ReviewBaseUrl = _svc.ReviewBaseUrl };
            vm.BatchId = batchId ?? vm.Batches.FirstOrDefault().Id;
            if (vm.BatchId > 0) vm.Gate = await _svc.GateAsync(vm.BatchId, ct);
            if (TempData["NrMsg"] is string m) { vm.Msg = m; vm.MsgOk = (TempData["NrOk"] as string) == "1"; }
            return View(vm);
        }

        [RequirePageAccess("SendNotices")]
        [HttpPost("/Notices/Send/Trigger"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Trigger(int batchId, CancellationToken ct)
        {
            var (ok, msg, token) = await _svc.TriggerAsync(batchId, User.Identity?.Name ?? "unknown", ct);
            // Hand over to eNotice: its review page prints/previews the notices, runs the PDF QA and sends.
            if (ok && !string.IsNullOrWhiteSpace(token))
                return Redirect($"{_svc.ReviewBaseUrl}/Section78Notice/Review/{token}");
            TempData["NrMsg"] = msg; TempData["NrOk"] = ok ? "1" : "0";
            return RedirectToAction(nameof(Index), new { batchId });
        }

        [RequirePageAccess("SendNotices")]
        [HttpGet("/Notices/Send/Status")]
        public async Task<IActionResult> Status(int batchId, CancellationToken ct) => Json(await _svc.StatusAsync(batchId, ct));
    }
}
