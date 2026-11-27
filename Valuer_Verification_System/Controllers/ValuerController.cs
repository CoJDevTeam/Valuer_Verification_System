using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Infrastructure.Configuration.Filters;
using Valuer_Verification_System.Models.Data;
using Valuer_Verification_System.Models.ViewModels;
using Valuer_Verification_System.Services.Valuer;

namespace Valuer_Verification_System.Controllers
{
    public sealed class ValuerController : Controller
    {
        private readonly VvsDbContext _db;
        private readonly IValuerVerificationService _svc;
        private readonly IValuerDashboardService _dash;
        private readonly IValuerRowDetailsService _details;
        private readonly IWeekRowStore _weekRows;

        public ValuerController(
            VvsDbContext db,
            IValuerVerificationService svc,
            IValuerDashboardService dash,
            IValuerRowDetailsService details,
            IWeekRowStore weekRows)
        {
            _weekRows = weekRows;
            _db = db;
            _svc = svc;
            _dash = dash;
            _details = details;
        }

        // ── Dashboard ─────────────────────────────────────────────────────
        [RequirePageAccess("ValuerDashboard")]
        [HttpGet("/Valuer/Dashboard")]
        public async Task<IActionResult> Dashboard(CancellationToken ct)
        {
            var valuer = User?.Identity?.Name ?? "UNKNOWN_VALUER";
            var vm = await _dash.BuildAsync(valuer, ct);
            return View(vm);
        }

        // ── Inbox ─────────────────────────────────────────────────────────
        [RequirePageAccess("ValuerInbox")]
        [HttpGet("/Valuer/Inbox")]
        public async Task<IActionResult> Inbox(
            int? batchId,
            string filter = "Pending",
            CancellationToken ct = default)
        {
            var valuer = (User?.Identity?.Name ?? "UNKNOWN_VALUER").Trim();

            var (batches, rows, unread, notes) =
                await _svc.GetInboxAsync(valuer, batchId, filter, ct);

            // ── Counts across ALL decisions (for the header chips) ─────────
            var allRows = await _weekRows.LoadForValuerAsync(
                valuer, batchId.HasValue && batchId.Value > 0 ? batchId : null, null, ct);

            // ── Classify: Single vs Sectional Title (bulk candidate) ───────
            //
            //   Single    — UnitType 00 / 0 / FH / RE (freehold)
            //   Bulk      — UnitType ST / SL / FL / LH / SS (sectional title)
            //               grouped by UnitKey; each unique UnitKey becomes
            //               ONE row entry with IsBulkGroup = true.
            //
            var singleRows = rows
                .Where(r => !IsSectionalTitle(r))
                .ToList();

            var sectionalRows = rows
                .Where(r => IsSectionalTitle(r))
                .ToList();

            // Group sectional rows by UnitKey
            var bulkGroups = sectionalRows
                .Where(r => !string.IsNullOrWhiteSpace(r.UnitKey))
                .GroupBy(r => r.UnitKey!.Trim())
                .ToList();

            // Sectional rows with no UnitKey fall back to single treatment
            var sectionalNoKey = sectionalRows
                .Where(r => string.IsNullOrWhiteSpace(r.UnitKey))
                .ToList();

            // ── Count bulk groups for the chip ─────────────────────────────
            int bulkGroupCount = bulkGroups.Count(g =>
                g.Any(r => r.Decision == VerificationDecision.Pending));

            // ── Build combined RowVm list ──────────────────────────────────
            var rowVms = new List<ValuerInboxVm.RowVm>();

            // Singles (freehold)
            foreach (var r in singleRows.Concat(sectionalNoKey))
            {
                rowVms.Add(new ValuerInboxVm.RowVm
                {
                    ExtractRowId = r.Id,
                    ValuationKey = r.ValuationKey,
                    Premise_ID = r.PremiseId ?? "",
                    Sector = r.Sector,
                    UnitType = r.UnitType ?? "FH",
                    PropertyDescription = r.PropertyDesc ?? r.PropertyDescription ?? "",
                    MarketValue = r.MarketValue,
                    Category = r.CatDesc ?? "",
                    Decision = r.Decision,
                    DecisionAtUtc = r.DecisionAtUtc,
                    DecisionComment = r.DecisionComment,
                    IsBulkGroup = false
                });
            }

            // Bulk groups (one row per UnitKey)
            foreach (var g in bulkGroups)
            {
                var rep = g.FirstOrDefault(r => r.Decision == VerificationDecision.Pending)
                                  ?? g.First();
                var pendingCount = g.Count(r => r.Decision == VerificationDecision.Pending);
                var yesCount = g.Count(r => r.Decision == VerificationDecision.Yes);
                var noCount = g.Count(r => r.Decision == VerificationDecision.No);
                var totalMv = g.Sum(r => r.MarketValue ?? 0);

                // Determine overall decision for the group display
                VerificationDecision groupDecision;
                if (pendingCount > 0)
                    groupDecision = VerificationDecision.Pending;
                else if (noCount > 0)
                    groupDecision = VerificationDecision.No;
                else
                    groupDecision = VerificationDecision.Yes;

                rowVms.Add(new ValuerInboxVm.RowVm
                {
                    ExtractRowId = rep.Id,
                    ValuationKey = rep.ValuationKey,
                    Premise_ID = rep.PremiseId ?? "",
                    Sector = rep.Sector,
                    UnitType = rep.UnitType ?? "ST",
                    PropertyDescription = rep.PropertyDesc ?? rep.PropertyDescription ?? "",
                    MarketValue = totalMv,         // sum for the group
                    Category = rep.CatDesc ?? "",
                    Decision = groupDecision,
                    DecisionAtUtc = rep.DecisionAtUtc,
                    DecisionComment = rep.DecisionComment,
                    IsBulkGroup = true,
                    BulkRowCount = g.Count(),
                    BulkPendingCount = pendingCount,
                    BulkYesCount = yesCount,
                    BulkNoCount = noCount,
                    BulkUnitKey = g.Key,
                    BulkPremiseId = rep.PremiseId ?? "",
                    BulkUnitNo = rep.UnitNo ?? "",
                    BulkTownNameDesc = rep.TownNameDesc ?? ""
                });
            }

            // Sort: bulk pending first, then singles by sector + valuation key
            rowVms = rowVms
                .OrderBy(r => r.IsBulkGroup
                    ? (r.Decision == VerificationDecision.Pending ? 0 : 1)
                    : 2)
                .ThenBy(r => r.Sector)
                .ThenBy(r => r.ValuationKey)
                .ToList();

            var vm = new ValuerInboxVm
            {
                ValuerName = valuer,
                BatchId = batchId,
                Filter = filter,
                Total = allRows.Count,
                Pending = allRows.Count(r => r.Decision == VerificationDecision.Pending),
                Yes = allRows.Count(r => r.Decision == VerificationDecision.Yes),
                No = allRows.Count(r => r.Decision == VerificationDecision.No),
                BulkGroupCount = bulkGroupCount,
                UnreadNotifications = unread,
                Notifications = notes.Select(n => new ValuerInboxVm.NotificationItemVm
                {
                    Id = n.Id,
                    Title = n.Title,
                    Message = n.Message,
                    ActionUrl = n.ActionUrl,
                    IsRead = n.IsRead,
                    CreatedAtUtc = n.CreatedAtUtc
                }).ToList(),
                Batches = batches.Select(b => new ValuerInboxVm.BatchOptionVm
                {
                    Id = b.Id,
                    BatchName = b.BatchName,
                    BatchDate = b.BatchDate,
                    WeekStart = b.WeekWindow.WeekStart,
                    WeekEnd = b.WeekWindow.WeekEnd,
                    NoticeSendDate = b.WeekWindow.NoticeSendDate
                }).ToList(),
                Rows = rowVms
            };

            if (batchId.HasValue)
                vm.BatchName = batches.FirstOrDefault(x => x.Id == batchId.Value)?.BatchName;

            return View(vm);
        }

        // ── Determine if a row is sectional title ─────────────────────────
        private static readonly HashSet<string> SectionalUnitTypes =
            new(StringComparer.OrdinalIgnoreCase) { "ST", "SL", "FL", "LH", "SS" };

        private static bool IsSectionalTitle(ExtractRow row)
        {
            var ut = row.UnitType?.Trim();
            if (!string.IsNullOrWhiteSpace(ut) && SectionalUnitTypes.Contains(ut))
                return true;

            // Fallback: check PremiseId for sectional suffix
            var pid = row.PremiseId?.ToUpperInvariant() ?? "";
            return SectionalUnitTypes.Any(s => pid.Contains(s));
        }

        // ── Notifications ─────────────────────────────────────────────────
        [HttpPost("/Valuer/Notifications/MarkRead")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkNotificationRead(
            long id, int? batchId, string filter = "Pending", CancellationToken ct = default)
        {
            var valuer = (User?.Identity?.Name ?? "UNKNOWN_VALUER").Trim();
            await _svc.MarkNotificationReadAsync(id, valuer, ct);
            return RedirectToAction(nameof(Inbox), new { batchId, filter });
        }

        [HttpPost("/Valuer/Notifications/MarkAllRead")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllNotificationsRead(
            int? batchId, string filter = "Pending", CancellationToken ct = default)
        {
            var valuer = (User?.Identity?.Name ?? "UNKNOWN_VALUER").Trim();
            await _svc.MarkAllNotificationsReadAsync(valuer, ct);
            return RedirectToAction(nameof(Inbox), new { batchId, filter });
        }

        // ── Single row verify ─────────────────────────────────────────────
        public sealed class VerifyRequest
        {
            public int ExtractRowId { get; set; }
            public string Decision { get; set; } = "";
            public string? Comment { get; set; }
            public int? BatchId { get; set; }
            public string? Filter { get; set; }
        }

        [HttpPost("/Valuer/Verify")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verify(VerifyRequest req, CancellationToken ct)
        {
            var valuer = User?.Identity?.Name ?? "UNKNOWN_VALUER";

            var decision = req.Decision?.Trim() switch
            {
                "Yes" => VerificationDecision.Yes,
                "No" => VerificationDecision.No,
                _ => VerificationDecision.Pending
            };

            try
            {
                await _svc.VerifyAsync(req.ExtractRowId, valuer, decision, req.Comment, ct);
                TempData["Success"] = "Verification saved.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Inbox),
                new { batchId = req.BatchId, filter = req.Filter ?? "Pending" });
        }

        // ── Row details (modal) ───────────────────────────────────────────
        [HttpGet("/Valuer/RowDetails")]
        public async Task<IActionResult> RowDetails(int extractRowId, CancellationToken ct)
        {
            var valuer = User?.Identity?.Name ?? "UNKNOWN_VALUER";
            try
            {
                var vm = await _svc.GetRowDetailsAsync(extractRowId, valuer, ct);
                return PartialView("_RowDetails", vm);
            }
            catch (Exception ex)
            {
                Response.StatusCode = 400;
                return Content(ex.Message);
            }
        }
    }
}