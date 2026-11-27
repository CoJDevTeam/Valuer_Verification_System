using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Services.Notice;
using Valuer_Verification_System.Services.Upload;

namespace Valuer_Verification_System.Controllers
{
    [Authorize]
    public sealed class ExtractApprovalController : Controller
    {
        private readonly VvsDbContext _db;
        private readonly IExtractUploadService _uploadService;
        private readonly IWeekTableSummaryService _summary;

        public ExtractApprovalController(
            VvsDbContext db,
            IExtractUploadService uploadService,
            IWeekTableSummaryService summary)
        {
            _summary = summary;
            _db = db;
            _uploadService = uploadService;
        }

        [HttpGet("/ExtractApproval/Batch/{batchId:int}")]
        public async Task<IActionResult> Batch(int batchId, CancellationToken ct)
        {
            var batch = await _db.Batches
                .Include(x => x.WeekWindow)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == batchId, ct);

            if (batch is null)
                return NotFound();

            List<ExtractApprovalSectorVm> sectorSummary;
            List<ExtractApprovalUnmatchedVm> unmatchedValuers;

            if (!string.IsNullOrWhiteSpace(batch.CleanWeekTableName))
            {
                // The week table is the working table: counts come from it.
                var ws = await _summary.GetAsync(batch.CleanWeekTableName!, ct);

                sectorSummary = ws.Sectors.Select(x => new ExtractApprovalSectorVm
                {
                    Sector = x.Sector,
                    TotalRows = x.TotalRows,
                    TotalProperties = x.TotalProperties,
                    TotalValuers = x.TotalValuers,
                    UnmatchedRows = x.UnmatchedRows
                }).ToList();

                unmatchedValuers = ws.Unmatched.Select(x => new ExtractApprovalUnmatchedVm
                {
                    Valuer = x.Valuer,
                    Rows = x.Rows,
                    Properties = x.Properties,
                    MatchStatus = x.MatchStatus,
                    MatchComment = x.MatchComment
                }).ToList();
            }
            else
            {
                sectorSummary = await _db.ExtractRows
                    .AsNoTracking()
                    .Where(x => x.BatchId == batchId)
                    .GroupBy(x => x.Sector ?? "Unassigned")
                    .Select(g => new ExtractApprovalSectorVm
                    {
                        Sector = g.Key,
                        TotalRows = g.Count(),
                        TotalProperties = g.Select(x => x.ValuationKey).Distinct().Count(),
                        TotalValuers = g.Where(x => x.Username != null && x.Username != "")
                                        .Select(x => x.Username)
                                        .Distinct()
                                        .Count(),
                        UnmatchedRows = g.Count(x => x.Username == null || x.Username == "")
                    })
                    .OrderBy(x => x.Sector)
                    .ToListAsync(ct);

                unmatchedValuers = await _db.ExtractRows
                    .AsNoTracking()
                    .Where(x =>
                        x.BatchId == batchId &&
                        (x.Username == null || x.Username == ""))
                    .GroupBy(x => x.Valuer ?? "Unknown")
                    .Select(g => new ExtractApprovalUnmatchedVm
                    {
                        Valuer = g.Key,
                        Rows = g.Count(),
                        Properties = g.Select(x => x.ValuationKey).Distinct().Count(),
                        MatchStatus = g.Select(x => x.ValuerMatchStatus).FirstOrDefault(),
                        MatchComment = g.Select(x => x.ValuerMatchComment).FirstOrDefault()
                    })
                    .OrderByDescending(x => x.Properties)
                    .ThenBy(x => x.Valuer)
                    .Take(50)
                    .ToListAsync(ct);

            }

            var vm = new ExtractApprovalVm
            {
                BatchId = batch.Id,
                BatchName = batch.BatchName,
                WeekStart = batch.WeekWindow.WeekStart,
                WeekEnd = batch.WeekWindow.WeekEnd,
                NoticeSendDate = batch.WeekWindow.NoticeSendDate,
                ReviewOpenDate = batch.WeekWindow.ReviewOpenDate,
                ReviewCloseDate = batch.WeekWindow.ReviewCloseDate,
                CleanWeekTableName = batch.CleanWeekTableName,
                ExtractCleanedAtUtc = batch.ExtractCleanedAtUtc,
                ExtractCleaningStatus = batch.ExtractCleaningStatus,
                ExtractCleaningMessage = batch.ExtractCleaningMessage,
                ProjectManagerNotifiedAtUtc = batch.ProjectManagerNotifiedAtUtc,
                ProjectManagerApprovedAtUtc = batch.ProjectManagerApprovedAtUtc,
                ProjectManagerApprovedBy = batch.ProjectManagerApprovedBy,
                ProjectManagerApprovalComment = batch.ProjectManagerApprovalComment,
                ManagersNotifiedAtUtc = batch.ManagersNotifiedAtUtc,
                ManagersNotifiedBy = batch.ManagersNotifiedBy,
                Sectors = sectorSummary,
                UnmatchedValuers = unmatchedValuers
            };

            return View(vm);
        }

        [HttpPost("/ExtractApproval/Batch/{batchId:int}/NotifySectorManagers")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NotifySectorManagers(
            int batchId,
            string? comment,
            CancellationToken ct)
        {
            var batch = await _db.Batches
                .FirstOrDefaultAsync(x => x.Id == batchId, ct);

            if (batch is null)
                return NotFound();

            if (batch.ExtractCleanedAtUtc == null)
            {
                TempData["ApprovalError"] = "Extract has not been cleaned yet.";
                return RedirectToAction(nameof(Batch), new { batchId });
            }

            if (!Valuer_Verification_System.Infrastructure.CleaningStatus.IsDone(batch.ExtractCleaningStatus, batch.ExtractCleanedAtUtc))
            {
                TempData["ApprovalError"] = "Extract cleaning is not completed successfully.";
                return RedirectToAction(nameof(Batch), new { batchId });
            }

            if (batch.ManagersNotifiedAtUtc != null)
            {
                TempData["ApprovalSuccess"] = "Sector Managers were already notified.";
                return RedirectToAction(nameof(Batch), new { batchId });
            }

            var approvedBy = User.Identity?.Name ?? "project-manager";

            if (batch.ProjectManagerApprovedAtUtc == null)
            {
                batch.ProjectManagerApprovedAtUtc = DateTime.UtcNow;
                batch.ProjectManagerApprovedBy = approvedBy;
                batch.ProjectManagerApprovalComment = comment;
                batch.UpdatedBy = approvedBy;
                batch.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync(ct);
            }

            try
            {
                await _uploadService.NotifyManagersAsync(
                    batchId,
                    approvedBy,
                    ct);

                TempData["ApprovalSuccess"] = "Sector Managers have been notified successfully.";
            }
            catch (Exception ex)
            {
                TempData["ApprovalError"] = ex.Message;
            }

            return RedirectToAction(nameof(Batch), new { batchId });
        }
    }

    public sealed class ExtractApprovalVm
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = "";

        public DateOnly WeekStart { get; set; }
        public DateOnly WeekEnd { get; set; }
        public DateOnly NoticeSendDate { get; set; }
        public DateOnly ReviewOpenDate { get; set; }
        public DateOnly ReviewCloseDate { get; set; }

        public string? CleanWeekTableName { get; set; }
        public DateTime? ExtractCleanedAtUtc { get; set; }
        public string? ExtractCleaningStatus { get; set; }
        public string? ExtractCleaningMessage { get; set; }

        public DateTime? ProjectManagerNotifiedAtUtc { get; set; }
        public DateTime? ProjectManagerApprovedAtUtc { get; set; }
        public string? ProjectManagerApprovedBy { get; set; }
        public string? ProjectManagerApprovalComment { get; set; }

        public DateTime? ManagersNotifiedAtUtc { get; set; }
        public string? ManagersNotifiedBy { get; set; }

        public List<ExtractApprovalSectorVm> Sectors { get; set; } = new();
        public List<ExtractApprovalUnmatchedVm> UnmatchedValuers { get; set; } = new();

        public int TotalRows => Sectors.Sum(x => x.TotalRows);
        public int TotalProperties => Sectors.Sum(x => x.TotalProperties);
        public int TotalValuers => Sectors.Sum(x => x.TotalValuers);
        public int TotalUnmatchedRows => Sectors.Sum(x => x.UnmatchedRows);

        public bool IsCleaned =>
            Valuer_Verification_System.Infrastructure.CleaningStatus.IsDone(ExtractCleaningStatus, ExtractCleanedAtUtc);

        public bool SectorManagersNotified => ManagersNotifiedAtUtc != null;
    }

    public sealed class ExtractApprovalSectorVm
    {
        public string Sector { get; set; } = "";
        public int TotalRows { get; set; }
        public int TotalProperties { get; set; }
        public int TotalValuers { get; set; }
        public int UnmatchedRows { get; set; }
    }

    public sealed class ExtractApprovalUnmatchedVm
    {
        public string Valuer { get; set; } = "";
        public int Rows { get; set; }
        public int Properties { get; set; }
        public string? MatchStatus { get; set; }
        public string? MatchComment { get; set; }
    }
}