using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Models.Data;
using Valuer_Verification_System.Models.ViewModels;
using Valuer_Verification_System.Services.Home;
using Valuer_Verification_System.Services.Notice;
using Valuer_Verification_System.Services.Stats;

namespace Valuer_Verification_System.Controllers
{
    public sealed class Section78StatsController : Controller
    {
        private readonly ISection78StatsService _stats;
        private readonly IDataTeamDashboardService _dashboard;
        private readonly VvsDbContext _db;

        public Section78StatsController(
            ISection78StatsService stats,
            IDataTeamDashboardService dashboard,
            VvsDbContext db)
        {
            _stats = stats;
            _dashboard = dashboard;
            _db = db;
        }

        // ── Main stats page — returns full DataTeamDashboardVm ────────────────
        [HttpGet("/DataTeam/Stats")]
        public async Task<IActionResult> Index(
            [FromQuery] string fy = "2025_26", CancellationToken ct = default)
        {
            // Build the full dashboard VM (which now includes Stats inside it)
            var vm = await _dashboard.BuildAsync(ct);

            // Override stats for the requested financial year
            vm.Stats = await _stats.BuildVmAsync(fy, ct);

            // Load available batches for the auto-populate dropdown
            var batches = await _db.Batches
                .Include(b => b.WeekWindow)
                .AsNoTracking()
                .OrderByDescending(b => b.Id)
                .Take(50)
                .ToListAsync(ct);

            var existingBatchIds = await _db.Section78Stats
                .AsNoTracking()
                .Where(s => s.BatchId.HasValue)
                .Select(s => s.BatchId!.Value)
                .ToHashSetAsync(ct);

            vm.Stats.AvailableBatches = batches.Select(b =>
            {
                var exDate = b.BatchDate;
                var wn = (int)Math.Ceiling(exDate.Day / 7.0);
                return new Section78StatsVm.BatchOption
                {
                    Id = b.Id,
                    BatchName = b.BatchName,
                    WeekTable = $"GV23_LIS_SV_{exDate.Year}_{exDate.Month:D2}_EXTRACT_W{wn}",
                    BatchDate = b.BatchDate,
                    HasStats = existingBatchIds.Contains(b.Id)
                };
            }).ToList();

            return View("~/Views/DataTeam/Section78Stats.cshtml", vm);
        }

        // ── Auto-populate from a batch ────────────────────────────────────────
        [HttpPost("/DataTeam/Stats/AutoPopulate/{batchId:int}")]
        public async Task<IActionResult> AutoPopulate(int batchId, CancellationToken ct)
        {
            var compiledBy = User.Identity?.Name ?? "DataTeam";
            try
            {
                var entry = await _stats.AutoPopulateFromBatchAsync(batchId, compiledBy, ct);
                return Json(new { success = true, id = entry.Id, msg = "Stats populated successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, msg = ex.Message });
            }
        }

        // ── Inline save of an entry (dates, reviews, closed) ─────────────────
        [HttpPost("/DataTeam/Stats/Save")]
        public async Task<IActionResult> Save([FromBody] SaveStatRequest req, CancellationToken ct)
        {
            if (req is null) return BadRequest();
            var updatedBy = User.Identity?.Name ?? "DataTeam";
            try
            {
                var entry = await _db.Section78Stats.FindAsync(new object[] { req.Id }, ct);
                if (entry is null) return NotFound();

                if (req.ReviewPeriodOpen.HasValue)
                    entry.ReviewPeriodOpen = DateOnly.FromDateTime(req.ReviewPeriodOpen.Value);
                if (req.ReviewPeriodClosed.HasValue)
                    entry.ReviewPeriodClosed = DateOnly.FromDateTime(req.ReviewPeriodClosed.Value);

                entry.ReviewsReceived = req.ReviewsReceived;
                entry.Closed = req.Closed;
                entry.CompiledBy = req.CompiledBy ?? entry.CompiledBy;
                entry.UpdatedBy = updatedBy;
                entry.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync(ct);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, msg = ex.Message });
            }
        }

        // ── Download Excel ────────────────────────────────────────────────────
        [HttpGet("/DataTeam/Stats/Download")]
        public async Task<IActionResult> Download(
            [FromQuery] string fy = "2025_26", CancellationToken ct = default)
        {
            var bytes = await _stats.GenerateExcelAsync(fy, ct);
            var name = $"Section78_Stats_{fy}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                name);
        }
    }

    // ── Request models ────────────────────────────────────────────────────────
    public sealed class SaveStatRequest
    {
        public int Id { get; set; }
        public DateTime? ReviewPeriodOpen { get; set; }
        public DateTime? ReviewPeriodClosed { get; set; }
        public int? ReviewsReceived { get; set; }
        public string? Closed { get; set; }
        public string? CompiledBy { get; set; }
    }
}