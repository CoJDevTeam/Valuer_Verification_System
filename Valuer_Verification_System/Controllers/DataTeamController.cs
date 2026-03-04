using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Domain.Dates;
using Valuer_Verification_System.Domain.WeekWindows;
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
        private readonly IWeekWindowService _weekService;
        private readonly INoticeCleaningService _cleaning;
        private readonly INoticePdfService _pdf;
        private readonly INoticePrintService _print;
        private readonly IWebHostEnvironment _env;

        public DataTeamController(
            VvsDbContext db,
            IWeekWindowCalculator calc,
            IWeekWindowService weekService,
            INoticeCleaningService cleaning,
            INoticePdfService pdf,
            INoticePrintService print,
            IWebHostEnvironment env)
        {
            _db = db;
            _calc = calc;
            _weekService = weekService;
            _cleaning = cleaning;
            _pdf = pdf;
            _print = print;
            _env = env;
        }

        // ═════════════════════════════════════════════════════════════════════
        // STEP 1 – Select week
        // ═════════════════════════════════════════════════════════════════════

        [HttpGet("/DataTeam/SendNotice")]
        public async Task<IActionResult> SendNotice([FromQuery] int? batchId, CancellationToken ct)
        {
            var vm = await BuildStep1Async(null, batchId, ct);
            return View(vm);
        }

        [HttpPost("/DataTeam/SendNotice/SelectWeek")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectWeek(int weekWindowId, CancellationToken ct)
        {
            var vm = await BuildStep1Async(weekWindowId, null, ct);
            return View("SendNotice", vm);
        }

        [HttpPost("/DataTeam/SendNotice/ProceedToClean")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProceedToClean(int batchId, CancellationToken ct)
        {
            var batch = await _db.Batches.Include(b => b.WeekWindow)
                                         .AsNoTracking()
                                         .FirstOrDefaultAsync(b => b.Id == batchId, ct);
            if (batch is null) return NotFound();

            var extractionDate = await GetExtractionDateAsync(batchId, batch.BatchDate, ct);

            var vm = new SendNoticeStep2Vm
            {
                BatchId = batch.Id,
                WeekWindowId = batch.WeekWindowId,
                ExtractionDate = extractionDate,
                NoticeSendDate = batch.WeekWindow.NoticeSendDate,
                WeekTableName = _cleaning.BuildWeekTableName(extractionDate)
            };
            return View("SendNoticeStep2", vm);
        }

        [HttpGet("/DataTeam/SendNotice/Step2/{batchId:int}")]
        public async Task<IActionResult> SendNoticeStep2(int batchId, CancellationToken ct)
            => await ProceedToClean(batchId, ct);

        // ═════════════════════════════════════════════════════════════════════
        // STEP 2 – Cleaning AJAX calls
        // ═════════════════════════════════════════════════════════════════════

        [HttpPost("/DataTeam/SendNotice/RunCleanStep")]
        public async Task<IActionResult> RunCleanStep([FromBody] RunCleanStepRequest req, CancellationToken ct)
        {
            if (req is null || string.IsNullOrWhiteSpace(req.StepName))
                return BadRequest(new { success = false, error = "Step name required." });

            var result = await _cleaning.RunStepAsync(
                req.StepName, req.ExtractionDate, req.LisMasterTable, req.SapTable, ct);
            return Json(result);
        }

        [HttpPost("/DataTeam/SendNotice/CreateWeekTable")]
        public async Task<IActionResult> CreateWeekTable([FromBody] CreateWeekTableRequest req, CancellationToken ct)
        {
            if (req is null || string.IsNullOrWhiteSpace(req.WeekTableName))
                return BadRequest(new { success = false, error = "Table name required." });

            var result = await _cleaning.CreateWeekTableAsync(req.WeekTableName, req.ExtractionDate, ct);
            return Json(result);
        }

        // ═════════════════════════════════════════════════════════════════════
        // STEP 3 – Print Notices
        // ═════════════════════════════════════════════════════════════════════

        [HttpGet("/DataTeam/SendNotice/Step3/{batchId:int}")]
        public async Task<IActionResult> PrintNotices(int batchId, CancellationToken ct)
        {
            var vm = await BuildPrintVmAsync(batchId, ct);
            return View("SendNoticeStep3", vm);
        }

        // AJAX: batch changed in dropdown — return fresh VM data as JSON
        [HttpGet("/DataTeam/SendNotice/PrintDetails/{batchId:int}")]
        public async Task<IActionResult> PrintDetails(int batchId, CancellationToken ct)
        {
            var batch = await _db.Batches.Include(b => b.WeekWindow)
                                         .AsNoTracking()
                                         .FirstOrDefaultAsync(b => b.Id == batchId, ct);
            if (batch is null) return NotFound();

            var extractionDate = await GetExtractionDateAsync(batchId, batch.BatchDate, ct);
            var ww = batch.WeekWindow;

            var counts = await GetNoticeCounts(batchId, ct);

            return Json(new
            {
                batchId,
                batchName = batch.BatchName,
                weekTableName = _cleaning.BuildWeekTableName(extractionDate),
                letterDate = ww.NoticeSendDate.ToString("dd MMMM yyyy").ToUpperInvariant(),
                reviewOpenDate = ww.ReviewOpenDate.ToString("dd MMMM yyyy").ToUpperInvariant(),
                reviewCloseDate = ww.ReviewCloseDate.ToString("dd MMMM yyyy").ToUpperInvariant(),
                noticeSendDateIso = ww.NoticeSendDate.ToString("yyyy-MM-dd"),
                totalNotices = counts.total,
                singleCount = counts.single,
                multiCount = counts.multi,
                outputFolder = _print.BuildOutputFolder(extractionDate, batch.BatchName,
                                      _cleaning.BuildWeekTableName(extractionDate))
            });
        }

        // AJAX: print one chunk
        [HttpPost("/DataTeam/SendNotice/PrintChunk")]
        public async Task<IActionResult> PrintChunk([FromBody] PrintChunkRequest req, CancellationToken ct)
        {
            if (req is null) return BadRequest();
            var result = await _print.PrintChunkAsync(
                req.BatchId, req.WeekTableName, req.Offset, req.ChunkSize, ct);
            return Json(result);
        }

        // GET: PDF preview (top-1 sendable property)
        [HttpGet("/DataTeam/SendNotice/PreviewPdf/{batchId:int}")]
        public async Task<IActionResult> PreviewPdf(int batchId, CancellationToken ct)
        {
            var batch = await _db.Batches.Include(b => b.WeekWindow)
                                         .AsNoTracking()
                                         .FirstOrDefaultAsync(b => b.Id == batchId, ct);
            if (batch is null) return NotFound();

            var ww = batch.WeekWindow;
            var ctx = new NoticeContext
            {
                LetterDate = ww.NoticeSendDate,
                ReviewOpenDate = ww.ReviewOpenDate,
                ReviewCloseDate = ww.ReviewCloseDate,
                LogoPath = Path.Combine(_env.WebRootPath, "Images", "City_of_Joburg_logo.png")
            };

            var bytes = await _pdf.GeneratePreviewAsync(batchId, ctx, _db, ct);
            if (bytes is null)
                return Content("No sendable rows found for this batch.", "text/plain");

            return File(bytes, "application/pdf");
        }

        // ═════════════════════════════════════════════════════════════════════
        // STEP 4 – Confirm & Send
        // ═════════════════════════════════════════════════════════════════════

        [HttpGet("/DataTeam/SendNotice/Step4/{batchId:int}")]
        public async Task<IActionResult> ConfirmSendPage(int batchId, [FromQuery] string? weekTableName, CancellationToken ct)
        {
            var batch = await _db.Batches.Include(b => b.WeekWindow)
                                         .AsNoTracking()
                                         .FirstOrDefaultAsync(b => b.Id == batchId, ct);
            if (batch is null) return NotFound();

            var ww = batch.WeekWindow;
            var extractionDate = await GetExtractionDateAsync(batchId, batch.BatchDate, ct);
            var tableName = weekTableName ?? _cleaning.BuildWeekTableName(extractionDate);

            var totalRows = await _db.ExtractRows.CountAsync(r => r.BatchId == batchId, ct);
            var sendCount = await _db.ExtractRows.CountAsync(r =>
                r.BatchId == batchId && r.SendNotice == "Y" && r.Decision == VerificationDecision.Yes, ct);

            var vm = new SendNoticeStep4Vm
            {
                BatchId = batch.Id,
                BatchName = batch.BatchName,
                WeekTableName = tableName,
                NoticeSendDate = ww.NoticeSendDate,
                ReviewOpenDate = ww.ReviewOpenDate,
                ReviewCloseDate = ww.ReviewCloseDate,
                TotalRows = totalRows,
                NoticeSendCount = sendCount,
                AlreadySent = batch.NoticesSent,
                SentAtUtc = batch.NoticesSentAtUtc,
                SentBy = batch.NoticesSentBy
            };

            return View("SendNoticeStep4", vm);
        }

        [HttpPost("/DataTeam/SendNotice/ConfirmSend")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmSend(int batchId, string weekTableName, CancellationToken ct)
        {
            var batch = await _db.Batches.Include(b => b.WeekWindow)
                                         .FirstOrDefaultAsync(b => b.Id == batchId, ct);
            if (batch is null) return NotFound();
            if (batch.NoticesSent) return RedirectToAction("ConfirmSendPage", new { batchId, weekTableName });

            var user = User.Identity?.Name ?? "DataTeam";

            batch.NoticesSent = true;
            batch.NoticesSentAtUtc = DateTime.UtcNow;
            batch.NoticesSentBy = user;
            batch.Status = BatchStatus.Sent;

            batch.WeekWindow.Status = WeekWindowStatus.Sent;

            await _db.SaveChangesAsync(ct);
            return Redirect("/");
        }

        // ═════════════════════════════════════════════════════════════════════
        // Helpers
        // ═════════════════════════════════════════════════════════════════════

        private async Task<PrintNoticeVm> BuildPrintVmAsync(int selectedBatchId, CancellationToken ct)
        {
            // All batches that are ReadyToSend or already Sent
            var batches = await _db.Batches
                .Include(b => b.WeekWindow)
                .AsNoTracking()
                .Where(b => b.Status == BatchStatus.ReadyToSend || b.Status == BatchStatus.Sent)
                .OrderByDescending(b => b.Id)
                .ToListAsync(ct);

            var options = new List<PrintNoticeVm.BatchOption>();
            foreach (var b in batches)
            {
                var exDate = await GetExtractionDateAsync(b.Id, b.BatchDate, ct);
                var count = await _db.ExtractRows.CountAsync(r =>
                    r.BatchId == b.Id && r.SendNotice == "Y" && r.Decision == VerificationDecision.Yes, ct);
                options.Add(new PrintNoticeVm.BatchOption
                {
                    Id = b.Id,
                    BatchName = b.BatchName,
                    WeekTableName = _cleaning.BuildWeekTableName(exDate),
                    NoticeSendDate = b.WeekWindow.NoticeSendDate,
                    NoticeCount = count
                });
            }

            // Fallback to first if not found
            if (selectedBatchId == 0 && options.Any()) selectedBatchId = options[0].Id;

            var selBatch = batches.FirstOrDefault(b => b.Id == selectedBatchId) ?? batches.FirstOrDefault();
            if (selBatch is null) return new PrintNoticeVm { AvailableBatches = options };

            var ww = selBatch.WeekWindow;
            var extractDate = await GetExtractionDateAsync(selBatch.Id, selBatch.BatchDate, ct);
            var tableName = _cleaning.BuildWeekTableName(extractDate);
            var cntTuple = await GetNoticeCounts(selBatch.Id, ct);

            return new PrintNoticeVm
            {
                AvailableBatches = options,
                SelectedBatchId = selBatch.Id,
                BatchName = selBatch.BatchName,
                WeekTableName = tableName,
                ExtractionDate = extractDate,
                NoticeSendDate = ww.NoticeSendDate,
                ReviewOpenDate = ww.ReviewOpenDate,
                ReviewCloseDate = ww.ReviewCloseDate,
                TotalNotices = cntTuple.total,
                SingleCount = cntTuple.single,
                MultiPurposeCount = cntTuple.multi,
                OutputFolderDisplay = _print.BuildOutputFolder(extractDate, selBatch.BatchName, tableName)
            };
        }

        private async Task<(int total, int single, int multi)> GetNoticeCounts(int batchId, CancellationToken ct)
        {
            var keys = await _db.ExtractRows
                .AsNoTracking()
                .Where(r => r.BatchId == batchId && r.SendNotice == "Y" && r.Decision == VerificationDecision.Yes)
                .GroupBy(r => r.ValuationKey)
                .Select(g => new
                {
                    Key = g.Key,
                    Count = g.Count(),
                    IsMulti = g.Any(r =>
                        r.CatDesc == "Multipurpose" && r.ValuationSplitInd != null && r.ValuationSplitInd != "")
                })
                .ToListAsync(ct);

            int multi = keys.Count(x => x.IsMulti || x.Count > 1);
            int single = keys.Count - multi;
            return (keys.Count, single, multi);
        }

        private async Task<SendNoticeStep1Vm> BuildStep1Async(int? weekWindowId, int? preselectedBatchId, CancellationToken ct)
        {
            var weeks = await _db.WeekWindows.AsNoTracking()
                .OrderByDescending(w => w.WeekStart).Take(52).ToListAsync(ct);

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

            // If batchId pre-selected, find its week
            if (preselectedBatchId.HasValue && !weekWindowId.HasValue)
            {
                var pb = await _db.Batches.AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Id == preselectedBatchId.Value, ct);
                if (pb != null) weekWindowId = pb.WeekWindowId;
            }

            vm.SelectedWeekWindowId = weekWindowId;

            if (weekWindowId.HasValue)
            {
                var batch = await _db.Batches.Include(b => b.WeekWindow)
                    .Where(b => b.WeekWindowId == weekWindowId.Value)
                    .OrderByDescending(b => b.Id).FirstOrDefaultAsync(ct);

                if (batch != null)
                {
                    vm.SelectedBatchId = batch.Id;

                    // Heal missing review dates (WeekWindows created before migration)
                    var ww = batch.WeekWindow;
                    if (ww.ReviewOpenDate == DateOnly.MinValue || ww.ReviewCloseDate == DateOnly.MinValue)
                    {
                        ww.ReviewOpenDate = _calc.ComputeReviewOpenDate(ww.NoticeSendDate);
                        ww.ReviewCloseDate = _calc.ComputeReviewCloseDate(ww.ReviewOpenDate);
                        await _db.SaveChangesAsync(ct);
                    }

                    var exDate = await GetExtractionDateAsync(batch.Id, batch.BatchDate, ct);
                    var counts = await _db.ExtractRows.AsNoTracking().Where(r => r.BatchId == batch.Id)
                        .GroupBy(r => 1).Select(g => new
                        {
                            Total = g.Count(),
                            Pending = g.Count(x => x.Decision == VerificationDecision.Pending),
                            Yes = g.Count(x => x.Decision == VerificationDecision.Yes),
                            No = g.Count(x => x.Decision == VerificationDecision.No)
                        }).FirstOrDefaultAsync(ct);

                    vm.Summary = new SendNoticeStep1Vm.BatchSummaryVm
                    {
                        BatchId = batch.Id,
                        BatchName = batch.BatchName,
                        BatchDate = batch.BatchDate,
                        ExtractionDate = exDate,
                        NoticeSendDate = batch.WeekWindow.NoticeSendDate,
                        ReviewOpenDate = batch.WeekWindow.ReviewOpenDate,
                        ReviewCloseDate = batch.WeekWindow.ReviewCloseDate,
                        Total = counts?.Total ?? 0,
                        Verified = (counts?.Yes ?? 0) + (counts?.No ?? 0),
                        Pending = counts?.Pending ?? 0
                    };
                }
            }
            return vm;
        }

        private async Task<DateOnly> GetExtractionDateAsync(int batchId, DateOnly fallback, CancellationToken ct)
        {
            return await _db.ExtractRows.AsNoTracking()
                .Where(r => r.BatchId == batchId && r.ExtractionDate.HasValue)
                .Select(r => r.ExtractionDate)
                .FirstOrDefaultAsync(ct) ?? fallback;
        }
    }

    // ── AJAX request bodies ────────────────────────────────────────────────────
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