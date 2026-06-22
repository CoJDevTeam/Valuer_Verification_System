using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Models.ViewModels;
using Valuer_Verification_System.Services.Upload;

namespace Valuer_Verification_System.Controllers
{
    public sealed class UploadController : Controller
    {
        private readonly VvsDbContext _db;
        private readonly IExtractUploadService _svc;

        public UploadController(VvsDbContext db, IExtractUploadService svc)
        {
            _db = db;
            _svc = svc;
        }

        private bool CanUseEmergencyUpload()
        {
            var role = User.FindFirst("Role")?.Value
                       ?? User.FindFirst("Position")?.Value
                       ?? User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                       ?? "";

            var name = User.Identity?.Name ?? "";

            return role.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                || role.Equals("Syscon", StringComparison.OrdinalIgnoreCase)
                || role.Contains("DEPUTY DIRECTOR", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Syscon", StringComparison.OrdinalIgnoreCase);
        }
        [HttpGet("/Upload")]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            if (!CanUseEmergencyUpload())
            {
                TempData["UploadBlocked"] =
                    "Manual extract upload has been disabled. Extracts are now imported automatically from the configured folder.";

                return RedirectToAction("Index", "Home");
            }
            var weeks = await _db.WeekWindows
                .OrderByDescending(x => x.WeekStart)
                .Take(200)
                .ToListAsync(ct);

            var vm = new UploadExtractVm
            {
                WeekWindows = weeks.Select(w => new UploadExtractVm.WeekWindowOptionVm
                {
                    Id = w.Id,
                    WeekStart = w.WeekStart,
                    WeekEnd = w.WeekEnd,
                    NoticeSendDate = w.NoticeSendDate
                }).ToList()
            };

            return View(vm);
        }

        [HttpPost("/Upload")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(UploadExtractVm vm, CancellationToken ct)
        {
            if (!CanUseEmergencyUpload())
                return Forbid();
            if (vm.File is null || vm.File.Length == 0)
            {
                ModelState.AddModelError("", "Please select an extract file.");
                return await Index(ct);
            }

            var user = User?.Identity?.Name ?? "system";

            try
            {
                var batchId = await _svc.UploadAsync(vm.WeekWindowId, vm.File, user, ct);
                TempData["Success"] = $"Upload successful. Batch created (ID: {batchId}).";
             
                return RedirectToAction(nameof(Review), new { batchId });

            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return await Index(ct);
            }
        }

        [HttpGet("/Upload/Review/{batchId:int}")]
        public async Task<IActionResult> Review(int batchId, CancellationToken ct)
        {
            var batch = await _db.Batches
                .Include(b => b.WeekWindow)
                .FirstOrDefaultAsync(b => b.Id == batchId, ct);

            if (batch is null) return NotFound();

            var rows = await _db.ExtractRows.Where(r => r.BatchId == batchId).ToListAsync(ct);

            var sectorCounts = rows
                .GroupBy(r => (r.Sector ?? "Unknown").Trim())
                .Select(g => new KeyValuePair<string, int>(g.Key, g.Count()))
                .OrderByDescending(x => x.Value)
                .ToList();

            var valuerCounts = rows
                .GroupBy(r => (r.Valuer ?? "Unassigned").Trim())
                .Select(g => new KeyValuePair<string, int>(g.Key, g.Count()))
                .OrderByDescending(x => x.Value)
                .ToList();

            var assigned = await _db.Assignments.CountAsync(a => a.ExtractRow.BatchId == batchId, ct);

            var vm = new UploadReviewVm
            {
                BatchId = batch.Id,
                BatchName = batch.BatchName,
                BatchDate = batch.BatchDate,
                WeekWindowId = batch.WeekWindowId,
                WeekStart = batch.WeekWindow.WeekStart,
                WeekEnd = batch.WeekWindow.WeekEnd,
                NoticeSendDate = batch.WeekWindow.NoticeSendDate,
                TotalRecords = rows.Count,
                AssignedRecords = assigned,
                ManagersNotified = batch.ManagersNotifiedAtUtc != null,
                ManagersNotifiedAtUtc = batch.ManagersNotifiedAtUtc,
                SectorCounts = sectorCounts,
                ValuerCounts = valuerCounts
            };

            return View(vm);
        }
        [HttpPost("/Upload/Review/{batchId:int}/Notify")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Notify(int batchId, CancellationToken ct)
        {
            var user = User?.Identity?.Name ?? "system";
            await _svc.NotifyManagersAsync(batchId, user, ct);

            TempData["Success"] = "Managers notified successfully.";
            return RedirectToAction(nameof(Review), new { batchId });
        }

    }
}
