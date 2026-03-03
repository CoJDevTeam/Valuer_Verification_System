using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Domain.Dates;
using Valuer_Verification_System.Models.Data;
using Valuer_Verification_System.Models.Notice;
using Valuer_Verification_System.Models.ViewModels;
using Valuer_Verification_System.Services.Notice;

namespace Valuer_Verification_System.Controllers
{
    public sealed class DataTeamController : Controller
    {
        private readonly VvsDbContext _db;
        private readonly IWeekWindowCalculator _calc;
        private readonly INoticeCleaningService _cleaning;
        private readonly INoticePdfService _pdf;
        private readonly IWebHostEnvironment _env;

        public DataTeamController(
            VvsDbContext db,
            IWeekWindowCalculator calc,
            INoticeCleaningService cleaning,
            INoticePdfService pdf,
            IWebHostEnvironment env)
        {
            _db = db;
            _calc = calc;
            _cleaning = cleaning;
            _pdf = pdf;
            _env = env;
        }

        // ═════════════════════════════════════════════════════════════════════
        // GET  /DataTeam/SendNotice
        // Step 1 – select week
        // ═════════════════════════════════════════════════════════════════════
        [HttpGet("/DataTeam/SendNotice")]
        public async Task<IActionResult> SendNotice(CancellationToken ct)
        {
            var vm = await BuildStep1Async(null, ct);
            return View(vm);
        }

        // ═════════════════════════════════════════════════════════════════════
        // POST /DataTeam/SendNotice/SelectWeek
        // Step 1 → load batch summary for chosen week
        // ═════════════════════════════════════════════════════════════════════
        [HttpPost("/DataTeam/SendNotice/SelectWeek")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectWeek(int weekWindowId, CancellationToken ct)
        {
            var vm = await BuildStep1Async(weekWindowId, ct);
            return View("SendNotice", vm);
        }

        // ═════════════════════════════════════════════════════════════════════
        // POST /DataTeam/SendNotice/ProceedToClean
        // Step 1 → Step 2 (open cleaning page)
        // ═════════════════════════════════════════════════════════════════════
        [HttpPost("/DataTeam/SendNotice/ProceedToClean")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProceedToClean(int batchId, CancellationToken ct)
        {
            var batch = await _db.Batches
                .Include(b => b.WeekWindow)
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == batchId, ct);

            if (batch is null) return NotFound();

            // Find extraction date from first row of this batch
            var extractionDate = await _db.ExtractRows
                .AsNoTracking()
                .Where(r => r.BatchId == batchId && r.ExtractionDate != null)
                .Select(r => r.ExtractionDate)
                .FirstOrDefaultAsync(ct) ?? DateOnly.FromDateTime(DateTime.Today);

            var noticeSendDate = batch.WeekWindow.NoticeSendDate;
            var reviewOpenDate = _calc.ComputeReviewOpenDate(noticeSendDate);
            var reviewCloseDate = _calc.ComputeReviewCloseDate(reviewOpenDate);

            var vm = new SendNoticeStep2Vm
            {
                BatchId = batch.Id,
                WeekWindowId = batch.WeekWindowId,
                ExtractionDate = extractionDate,
                NoticeSendDate = noticeSendDate,
                WeekTableName = _cleaning.BuildWeekTableName(extractionDate)
            };

            return View("SendNoticeStep2", vm);
        }

        // ═════════════════════════════════════════════════════════════════════
        // GET  /DataTeam/SendNotice/Step2/{batchId}
        // Direct link to Step 2 (e.g. after page refresh)
        // ═════════════════════════════════════════════════════════════════════
        [HttpGet("/DataTeam/SendNotice/Step2/{batchId:int}")]
        public async Task<IActionResult> SendNoticeStep2(int batchId, CancellationToken ct)
        {
            return await ProceedToClean(batchId, ct);
        }

        // ═════════════════════════════════════════════════════════════════════
        // POST /DataTeam/SendNotice/RunCleanStep   (AJAX)
        // Called per-step by the progress UI
        // ═════════════════════════════════════════════════════════════════════
        [HttpPost("/DataTeam/SendNotice/RunCleanStep")]
        public async Task<IActionResult> RunCleanStep(
            [FromBody] RunCleanStepRequest req,
            CancellationToken ct)
        {
            if (req is null || string.IsNullOrWhiteSpace(req.StepName))
                return BadRequest(new { success = false, error = "Step name required." });

            var result = await _cleaning.RunStepAsync(
                req.StepName,
                req.ExtractionDate,
                req.LisMasterTable,
                req.SapTable,
                ct);

            return Json(result);
        }

        // ═════════════════════════════════════════════════════════════════════
        // POST /DataTeam/SendNotice/CreateWeekTable   (AJAX)
        // Final step: create the GV23 table on 173
        // ═════════════════════════════════════════════════════════════════════
        [HttpPost("/DataTeam/SendNotice/CreateWeekTable")]
        public async Task<IActionResult> CreateWeekTable(
            [FromBody] CreateWeekTableRequest req,
            CancellationToken ct)
        {
            if (req is null || string.IsNullOrWhiteSpace(req.WeekTableName))
                return BadRequest(new { success = false, error = "Table name required." });

            var result = await _cleaning.CreateWeekTableAsync(
                req.WeekTableName,
                req.ExtractionDate,
                ct);

            return Json(result);
        }

        // ═════════════════════════════════════════════════════════════════════
        // GET  /DataTeam/SendNotice/DownloadNotice/{rowId}   (single preview)
        // ═════════════════════════════════════════════════════════════════════
        [HttpGet("/DataTeam/SendNotice/DownloadNotice/{rowId:int}")]
        public async Task<IActionResult> DownloadNotice(int rowId, CancellationToken ct)
        {
            var row = await _db.ExtractRows
                .AsNoTracking()
                .Include(r => r.Batch).ThenInclude(b => b.WeekWindow)
                .FirstOrDefaultAsync(r => r.Id == rowId, ct);

            if (row is null) return NotFound();

            var noticeSendDate = row.Batch.WeekWindow.NoticeSendDate;
            var reviewOpenDate = _calc.ComputeReviewOpenDate(noticeSendDate);
            var reviewCloseDate = _calc.ComputeReviewCloseDate(reviewOpenDate);

            var ctx = new NoticeContext
            {
                LetterDate = noticeSendDate,
                ReviewOpenDate = reviewOpenDate,
                ReviewCloseDate = reviewCloseDate,
                LogoPath = Path.Combine(_env.WebRootPath, "Images", "City_of_Joburg_logo.png")
            };

            var bytes = _pdf.GenerateNotice(row, ctx);
            var fileName = $"Notice_{row.ValuationKey}.pdf";

            return File(bytes, "application/pdf", fileName);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private async Task<SendNoticeStep1Vm> BuildStep1Async(int? weekWindowId, CancellationToken ct)
        {
            var weeks = await _db.WeekWindows
                .AsNoTracking()
                .OrderByDescending(w => w.WeekStart)
                .Take(52)
                .ToListAsync(ct);

            var vm = new SendNoticeStep1Vm
            {
                Weeks = weeks.Select(w => new SendNoticeStep1Vm.WeekOptionVm
                {
                    Id = w.Id,
                    WeekStart = w.WeekStart,
                    WeekEnd = w.WeekEnd,
                    NoticeSendDate = w.NoticeSendDate
                }).ToList(),
                SelectedWeekWindowId = weekWindowId
            };

            if (weekWindowId.HasValue)
            {
                // Load the batch for this week
                var batch = await _db.Batches
                    .AsNoTracking()
                    .Include(b => b.WeekWindow)
                    .Where(b => b.WeekWindowId == weekWindowId.Value)
                    .OrderByDescending(b => b.Id)
                    .FirstOrDefaultAsync(ct);

                if (batch != null)
                {
                    vm.SelectedBatchId = batch.Id;

                    // Extraction date from rows
                    var extractionDate = await _db.ExtractRows
                        .AsNoTracking()
                        .Where(r => r.BatchId == batch.Id && r.ExtractionDate != null)
                        .Select(r => r.ExtractionDate)
                        .FirstOrDefaultAsync(ct) ?? batch.BatchDate;

                    // Counts
                    var counts = await _db.ExtractRows
                        .AsNoTracking()
                        .Where(r => r.BatchId == batch.Id)
                        .GroupBy(r => 1)
                        .Select(g => new
                        {
                            Total = g.Count(),
                            Pending = g.Count(x => x.Decision == VerificationDecision.Pending),
                            Yes = g.Count(x => x.Decision == VerificationDecision.Yes),
                            No = g.Count(x => x.Decision == VerificationDecision.No)
                        })
                        .FirstOrDefaultAsync(ct);

                    var total = counts?.Total ?? 0;
                    var pending = counts?.Pending ?? 0;
                    var verified = (counts?.Yes ?? 0) + (counts?.No ?? 0);

                    var noticeSendDate = batch.WeekWindow.NoticeSendDate;
                    var reviewOpenDate = _calc.ComputeReviewOpenDate(noticeSendDate);
                    var reviewCloseDate = _calc.ComputeReviewCloseDate(reviewOpenDate);

                    vm.Summary = new SendNoticeStep1Vm.BatchSummaryVm
                    {
                        BatchId = batch.Id,
                        BatchName = batch.BatchName,
                        BatchDate = batch.BatchDate,
                        ExtractionDate = extractionDate,
                        NoticeSendDate = noticeSendDate,
                        ReviewOpenDate = reviewOpenDate,
                        ReviewCloseDate = reviewCloseDate,
                        Total = total,
                        Verified = verified,
                        Pending = pending
                    };
                }
            }

            return vm;
        }
    }

    // ── AJAX request bodies ───────────────────────────────────────────────────
    public sealed class RunCleanStepRequest
    {
        public string StepName { get; set; } = "";
        public DateOnly ExtractionDate { get; set; }
        public string LisMasterTable { get; set; } = "";
        public string SapTable { get; set; } = "";
    }

    public sealed class CreateWeekTableRequest
    {
        public string WeekTableName { get; set; } = "";
        public DateOnly ExtractionDate { get; set; }
    }
}