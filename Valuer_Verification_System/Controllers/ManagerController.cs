using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Infrastructure.Configuration.Filters;
using Valuer_Verification_System.Models.ViewModels;
using Valuer_Verification_System.Services.Manager;
using Valuer_Verification_System.Services.Stats;

namespace Valuer_Verification_System.Controllers
{
    public sealed class ManagerController : Controller
    {
        private readonly VvsDbContext _db;
        private readonly IManagerReassignmentService _svc;
        private readonly IManagerDashboardService _dash;
        private readonly IStatsExportService _stats;
        public ManagerController(VvsDbContext db, IManagerReassignmentService svc, IManagerDashboardService dash,IStatsExportService stats)
        {
            _db = db;
            _svc = svc;
            _dash = dash;
            _stats = stats;
        }
        [RequirePageAccess("ManagerDashboard")]
        [HttpGet("/Manager/Dashboard")]
        public async Task<IActionResult> Dashboard(int? batchId, CancellationToken ct)
        {
            var vm = await _dash.BuildAsync(batchId, ct);
            return View(vm);
        }
        [RequirePageAccess("ManagerDashboard")]
        [HttpGet("/Manager/Reassign")]
        public async Task<IActionResult> Reassign(int? batchId, string? currentValuer, string? sector, int top = 500, CancellationToken ct = default)
        {
            var batches = await _db.Batches
                .OrderByDescending(b => b.Id)
                .Take(200)
                .Select(b => new ReassignVm.BatchVm
                {
                    Id = b.Id,
                    BatchName = b.BatchName,
                    BatchDate = b.BatchDate
                })
                .ToListAsync(ct);

            var selectedBatchId = batchId ?? batches.FirstOrDefault()?.Id ?? 0;

            var valuers = await _svc.GetValuerListAsync(selectedBatchId, ct);

            // Sectors dropdown from batch rows
            var sectors = await _db.ExtractRows
                .AsNoTracking()
                .Where(r => r.BatchId == selectedBatchId)
                .Select(r => r.Sector ?? "Unknown")
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync(ct);

            var rows = selectedBatchId > 0
                ? await _svc.ListPendingAsync(selectedBatchId, currentValuer, sector, top, ct)
                : new();

            var vm = new ReassignVm
            {
                BatchId = selectedBatchId,
                CurrentValuer = currentValuer,
                Sector = sector,
                Top = top,
                Batches = batches,
                Valuers = valuers,
                Sectors = sectors,
                Rows = rows.Select(r => new ReassignVm.RowVm
                {
                    ExtractRowId = r.ExtractRowId,
                    ValuationKey = r.ValuationKey,
                    Sector = r.Sector,
                    PropertyDescription = r.PropertyDescription,
                    CurrentValuer = r.CurrentValuer
                }).ToList()
            };

            return View(vm);
        }

        public sealed class ReassignPost
        {
            public int BatchId { get; set; }
            public int ExtractRowId { get; set; }
            public string FromValuer { get; set; } = "";
            public string ToValuer { get; set; } = "";
            public string Reason { get; set; } = "";

            public string? CurrentValuer { get; set; }
            public string? Sector { get; set; }
            public int Top { get; set; } = 500;
        }

        [HttpPost("/Manager/Reassign")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reassign(ReassignPost post, CancellationToken ct)
        {
            var manager = User?.Identity?.Name ?? "MANAGER";

            try
            {
                await _svc.ReassignAsync(post.ExtractRowId, post.FromValuer, post.ToValuer, post.Reason, manager, ct);
                TempData["Success"] = "Record reassigned successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Reassign), new
            {
                batchId = post.BatchId,
                currentValuer = post.CurrentValuer,
                sector = post.Sector,
                top = post.Top
            });
        }
        [HttpPost("/Manager/Reassign/Bulk")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkReassign(BulkReassignPost post, CancellationToken ct)
        {
            var manager = User?.Identity?.Name ?? "MANAGER";

            try
            {
                var res = await _svc.BulkReassignAsync(
                    post.BatchId,
                    post.FromValuer,
                    post.ToValuer,
                    post.Reason,
                    manager,
                    ct);

                TempData["Success"] = $"Bulk reassignment complete. Found: {res.TotalFound}, Moved: {res.Moved}, Skipped (not pending): {res.SkippedNotPending}.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Reassign), new
            {
                batchId = post.BatchId,
                currentValuer = post.CurrentValuer,
                sector = post.Sector,
                top = post.Top
            });
        }
        [RequirePageAccess("ManagerDashboard")]
        [HttpGet("/Manager/Stats/Generate")]
        public async Task<IActionResult> GenerateStats(int batchId, CancellationToken ct)
        {
            var user = User?.Identity?.Name ?? "system";

            try
            {
                var res = await _stats.GenerateBatchStatsAsync(batchId, user, ct);
                TempData["Success"] = $"Stats generated and saved: {res.FileName}";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Dashboard", "Manager", new { batchId });
        }

        [HttpGet("/Manager/Stats/Download")]
        public async Task<IActionResult> DownloadStats(int batchId, CancellationToken ct)
        {
            var user = User?.Identity?.Name ?? "system";

            var res = await _stats.GenerateBatchStatsAsync(batchId, user, ct);

            return File(
                res.Bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                res.FileName);
        }
    }
}
