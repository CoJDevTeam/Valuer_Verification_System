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

        public ValuerController(VvsDbContext db, IValuerVerificationService svc, IValuerDashboardService dash, IValuerRowDetailsService details)
        {
            _db = db;
            _svc = svc;
            _dash = dash;
            _details = details;
        }

        [RequirePageAccess("ValuerDashboard")]
        [HttpGet("/Valuer/Dashboard")]
        public async Task<IActionResult> Dashboard(CancellationToken ct)
        {
            var valuer = User?.Identity?.Name ?? "UNKNOWN_VALUER";
            var vm = await _dash.BuildAsync(valuer, ct);
            return View(vm);
        }

        [RequirePageAccess("ValuerInbox")]
        [HttpGet("/Valuer/Inbox")]
        public async Task<IActionResult> Inbox(int? batchId, string filter = "Pending", CancellationToken ct = default)
        {
            var valuer = (User?.Identity?.Name ?? "UNKNOWN_VALUER").Trim();

            // Load inbox rows + relevant batches for dropdown + notifications
            var (batches, rows, unread, notes) = await _svc.GetInboxAsync(valuer, batchId, filter, ct);

            // Counts (for header stats)
            var allRowsForCounts = await _db.Assignments
                .AsNoTracking()
                .Where(a => a.AssignedToValuer == valuer && (!batchId.HasValue || a.ExtractRow.BatchId == batchId.Value))
                .Select(a => a.ExtractRow)
                .ToListAsync(ct);

            var vm = new ValuerInboxVm
            {
                ValuerName = valuer,
                BatchId = batchId,
                Filter = filter,

                Total = allRowsForCounts.Count,
                Pending = allRowsForCounts.Count(r => r.Decision == VerificationDecision.Pending),
                Yes = allRowsForCounts.Count(r => r.Decision == VerificationDecision.Yes),
                No = allRowsForCounts.Count(r => r.Decision == VerificationDecision.No),

                // ✅ notifications
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

                Rows = rows.Select(r => new ValuerInboxVm.RowVm
                {
                    ExtractRowId = r.Id,
                    ValuationKey = r.ValuationKey,
                    Sector = r.Sector,
                    PropertyDescription = r.PropertyDescription,
                    MarketValue = r.MarketValue,
                    Category = r.Category,
                    PropertyType = r.PropertyType,
                    Decision = r.Decision,
                    DecisionAtUtc = r.DecisionAtUtc,
                    DecisionComment = r.DecisionComment,
                    AssignedToValuer = valuer
                }).ToList()
            };

            if (batchId.HasValue)
            {
                var b = batches.FirstOrDefault(x => x.Id == batchId.Value);
                vm.BatchName = b?.BatchName;
            }

            return View(vm);
        }

        [HttpPost("/Valuer/Notifications/MarkRead")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkNotificationRead(long id, int? batchId, string filter = "Pending", CancellationToken ct = default)
        {
            var valuer = (User?.Identity?.Name ?? "UNKNOWN_VALUER").Trim();
            await _svc.MarkNotificationReadAsync(id, valuer, ct);

            return RedirectToAction(nameof(Inbox), new { batchId, filter });
        }

        [HttpPost("/Valuer/Notifications/MarkAllRead")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllNotificationsRead(int? batchId, string filter = "Pending", CancellationToken ct = default)
        {
            var valuer = (User?.Identity?.Name ?? "UNKNOWN_VALUER").Trim();
            await _svc.MarkAllNotificationsReadAsync(valuer, ct);

            return RedirectToAction(nameof(Inbox), new { batchId, filter });
        }

        public sealed class VerifyRequest
        {
            public int ExtractRowId { get; set; }
            public string Decision { get; set; } = ""; // "Yes" or "No"
            public string? Comment { get; set; }
            public int? BatchId { get; set; } // so we can redirect back properly
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

            return RedirectToAction(nameof(Inbox), new { batchId = req.BatchId, filter = req.Filter ?? "Pending" });
        }

        

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
