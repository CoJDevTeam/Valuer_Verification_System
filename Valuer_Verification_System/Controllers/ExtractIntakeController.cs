using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Infrastructure.Configuration;
using Valuer_Verification_System.Models.Data;
using Valuer_Verification_System.Models.ViewModels;
using Valuer_Verification_System.Services.Notice;
using Valuer_Verification_System.Services.Upload;

namespace Valuer_Verification_System.Controllers
{
    /// <summary>
    /// Data Team extract flow, one page per step:
    ///   Step 1  /ExtractIntake                 see the extracts in the folder and import new ones
    ///   Step 2  /ExtractIntake/Clean/{id}      create the week table + clean it
    ///   Step 3  /ExtractIntake/Notify/{id}     email the managers
    ///   Step 4  (send day)                     print and send - built separately
    /// </summary>
    [Authorize]
    public sealed class ExtractIntakeController : Controller
    {
        private readonly VvsDbContext _db;
        private readonly IConfiguration _config;
        private readonly IExtractFolderImportService _folderImport;
        private readonly IExtractFolderImportLock _folderImportLock;
        private readonly INoticeCleaningService _cleaning;
        private readonly ILatestCleaningTableResolver _latestTables;
        private readonly IWeekTableSummaryService _summary;
        private readonly IExtractApprovalNotificationService _approvalNotify;
        private readonly ExtractInboxSettings _settings;
        private readonly EmailSettings _emailCfg;
        private readonly IAppMode _mode;

        public ExtractIntakeController(
            VvsDbContext db,
            IConfiguration config,
            IExtractFolderImportService folderImport,
            IExtractFolderImportLock folderImportLock,
            INoticeCleaningService cleaningService,
            ILatestCleaningTableResolver latestTables,
            IWeekTableSummaryService summary,
            IExtractApprovalNotificationService approvalNotify,
            IOptions<ExtractInboxSettings> settings,
            IOptions<EmailSettings> emailCfg,
            IAppMode mode)
        {
            _mode = mode;
            _db = db;
            _config = config;
            _folderImport = folderImport;
            _folderImportLock = folderImportLock;
            _cleaning = cleaningService;
            _latestTables = latestTables;
            _summary = summary;
            _approvalNotify = approvalNotify;
            _settings = settings.Value;
            _emailCfg = emailCfg.Value;
        }

        // ── who may run the steps ────────────────────────────────────────────

        private bool IsDataTeamUser()
        {
            var position = User.FindFirst("Position")?.Value
                ?? User.FindFirst("Role")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                ?? "";

            var userName = User.Identity?.Name ?? "";

            return position.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                || position.Equals("Syscon", StringComparison.OrdinalIgnoreCase)
                || position.Contains("DEPUTY DIRECTOR", StringComparison.OrdinalIgnoreCase)
                || position.Contains("DATA", StringComparison.OrdinalIgnoreCase)
                || userName.Contains("10112533", StringComparison.OrdinalIgnoreCase)
                || userName.Contains("Syscon", StringComparison.OrdinalIgnoreCase);
        }

        private bool CanRunFolderImportNow() => _settings.AllowManualRun && IsDataTeamUser();

        // ═════════════════════════════════════════════════════════════════════
        // STEP 1 - see the extracts in the folder, import new ones
        // ═════════════════════════════════════════════════════════════════════

        [HttpGet("/ExtractIntake")]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var folderPath = _config["ExtractInbox:FolderPath"] ?? "";
            var folderExists = !string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath);

            var files = new List<ExtractFolderFileVm>();

            if (folderExists)
            {
                files = Directory.GetFiles(folderPath)
                    .Where(x =>
                        x.EndsWith(".xls", StringComparison.OrdinalIgnoreCase) ||
                        x.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ||
                        x.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    .Select(x =>
                    {
                        var info = new FileInfo(x);
                        return new ExtractFolderFileVm
                        {
                            FileName = info.Name,
                            FullPath = info.FullName,
                            SizeBytes = info.Length,
                            LastModified = info.LastWriteTime
                        };
                    })
                    .OrderByDescending(x => x.LastModified)
                    .Take(50)
                    .ToList();
            }

            // which of these files are already imported, and as which batch
            var names = files.Select(f => f.FileName).ToList();

            var imported = await _db.ExtractFiles
                .AsNoTracking()
                .Where(x => names.Contains(x.OriginalFileName))
                .Select(x => new { x.OriginalFileName, x.BatchId, x.Batch.BatchName })
                .ToListAsync(ct);

            foreach (var f in files)
            {
                var hit = imported.FirstOrDefault(x =>
                    x.OriginalFileName.Equals(f.FileName, StringComparison.OrdinalIgnoreCase));

                if (hit is not null)
                {
                    f.Imported = true;
                    f.BatchId = hit.BatchId;
                    f.BatchName = hit.BatchName;
                }
            }

            var batches = await _db.Batches
                .AsNoTracking()
                .OrderByDescending(b => b.Id)
                .Take(30)
                .Select(b => new ExtractIntakeBatchVm
                {
                    BatchId = b.Id,
                    BatchName = b.BatchName,
                    BatchDate = b.BatchDate,
                    Status = b.Status.ToString(),
                    WeekStart = b.WeekWindow.WeekStart,
                    WeekEnd = b.WeekWindow.WeekEnd,
                    NoticeSendDate = b.WeekWindow.NoticeSendDate,
                    ReviewOpenDate = b.WeekWindow.ReviewOpenDate,
                    ReviewCloseDate = b.WeekWindow.ReviewCloseDate,
                    ExtractCleanedAtUtc = b.ExtractCleanedAtUtc,
                    ExtractCleanedBy = b.ExtractCleanedBy,
                    CleanWeekTableName = b.CleanWeekTableName,
                    ExtractCleaningStatus = b.ExtractCleaningStatus,
                    ExtractCleaningMessage = b.ExtractCleaningMessage,
                    ProjectManagerNotifiedAtUtc = b.ProjectManagerNotifiedAtUtc,
                    ProjectManagerApprovedAtUtc = b.ProjectManagerApprovedAtUtc,
                    ManagersNotifiedAtUtc = b.ManagersNotifiedAtUtc
                })
                .ToListAsync(ct);

            var batchIds = batches.Select(x => x.BatchId).ToList();

            var fileLogs = await _db.ExtractFiles
                .AsNoTracking()
                .Where(x => batchIds.Contains(x.BatchId))
                .Select(x => new { x.BatchId, x.OriginalFileName, x.FileSizeBytes, x.UploadedAtUtc })
                .ToListAsync(ct);

            // raw row counts (ExtractRows = untouched backup)
            var rawCounts = await _db.ExtractRows
                .AsNoTracking()
                .Where(x => batchIds.Contains(x.BatchId))
                .GroupBy(x => x.BatchId)
                .Select(g => new { BatchId = g.Key, Rows = g.Count() })
                .ToListAsync(ct);

            foreach (var b in batches)
            {
                var file = fileLogs.FirstOrDefault(x => x.BatchId == b.BatchId);
                if (file is not null)
                {
                    b.OriginalFileName = file.OriginalFileName;
                    b.FileSizeBytes = file.FileSizeBytes;
                    b.UploadedAtUtc = file.UploadedAtUtc;
                }

                b.TotalRows = rawCounts.FirstOrDefault(x => x.BatchId == b.BatchId)?.Rows ?? 0;
            }

            var audits = await _db.ExtractFolderImportAudits
                .AsNoTracking()
                .OrderByDescending(x => x.StartedAtUtc)
                .Take(10)
                .Select(x => new ExtractFolderImportAuditVm
                {
                    Id = x.Id,
                    RunType = x.RunType,
                    StartedBy = x.StartedBy,
                    StartedAtUtc = x.StartedAtUtc,
                    FinishedAtUtc = x.FinishedAtUtc,
                    Success = x.Success,
                    ImportedCount = x.ImportedCount,
                    SkippedCount = x.SkippedCount,
                    FailedCount = x.FailedCount,
                    Message = x.Message,
                    ErrorMessage = x.ErrorMessage,
                    FolderPath = x.FolderPath,
                    MachineName = x.MachineName
                })
                .ToListAsync(ct);

            return View(new ExtractIntakeIndexVm
            {
                FolderPath = folderPath,
                FolderExists = folderExists,
                CanRunFolderImportNow = CanRunFolderImportNow(),
                ImportIsRunning = _folderImportLock.IsRunning,
                ImportStartedAtUtc = _folderImportLock.StartedAtUtc,
                ImportStartedBy = _folderImportLock.StartedBy,
                FilesInFolder = files,
                Batches = batches,
                Audits = audits
            });
        }

        [HttpPost("/ExtractIntake/RunNow")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunNow(CancellationToken ct)
        {
            if (!CanRunFolderImportNow())
                return Forbid();

            try
            {
                var result = await _folderImport.ImportNewFilesAsync(
                    runType: "Manual",
                    startedBy: User.Identity?.Name ?? "manual-user",
                    ct: ct);

                if (result.ImportedCount == 0 && result.FailedCount == 0 && result.SkippedCount == 1 && _folderImportLock.IsRunning)
                {
                    TempData["ExtractIntakeError"] =
                        "A folder import is already running. Please wait for it to finish.";
                }
                else if (result.FailedCount > 0)
                {
                    TempData["ExtractIntakeError"] =
                        $"Folder check finished with errors. New: {result.ImportedCount}, already imported: {result.SkippedCount}, failed: {result.FailedCount}. " +
                        string.Join(" | ", result.Messages.Where(m => m.Contains("failed", StringComparison.OrdinalIgnoreCase)).Take(3));
                }
                else
                {
                    TempData["ExtractIntakeSuccess"] =
                        $"Folder checked. New extracts imported: {result.ImportedCount}. Already imported: {result.SkippedCount}.";
                }
            }
            catch (Exception ex)
            {
                TempData["ExtractIntakeError"] = "Folder import failed: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // ═════════════════════════════════════════════════════════════════════
        // STEP 2 - create the week table and clean it
        // ═════════════════════════════════════════════════════════════════════

        [HttpGet("/ExtractIntake/Clean/{batchId:int}")]
        public async Task<IActionResult> Clean(int batchId, CancellationToken ct)
        {
            var vm = await BuildStepVmAsync(batchId, ct);
            if (vm is null)
                return NotFound();

            vm.Steps = await _db.CleaningStepAudits
                .AsNoTracking()
                .Where(x => x.BatchId == batchId && x.StepName.StartsWith("IMPORT:"))
                .OrderByDescending(x => x.Id)
                .Take(10)
                .Select(x => new ExtractStepAuditVm
                {
                    StepName = x.StepName.Substring(7),
                    FinishedAtUtc = x.FinishedAtUtc,
                    DurationMs = x.DurationMs,
                    Success = x.Success,
                    RowsAffected = x.RowsAffected,
                    ErrorMessage = x.ErrorMessage
                })
                .ToListAsync(ct);

            vm.Steps.Reverse();

            if (!string.IsNullOrWhiteSpace(vm.CleanWeekTableName) && vm.IsCleaned)
                vm.Summary = await TryGetSummaryAsync(vm.CleanWeekTableName!, ct);

            return View(vm);
        }

        [HttpPost("/ExtractIntake/Clean/{batchId:int}/Run")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CleanRun(int batchId, CancellationToken ct)
        {
            if (!IsDataTeamUser())
                return Forbid();

            try
            {
                var by = User.Identity?.Name ?? "data-team";
                var latest = await _latestTables.ResolveAsync(ct);

                var result = await _cleaning.ImportWeekTableAndCleanAsync(
                    batchId, by, latest.LisTable, latest.SapTable, ct);

                if (!result.Success)
                {
                    TempData["ExtractIntakeError"] = result.Message;
                }
                else
                {
                    var message = result.Message;

                    // Development / Testing only: each person (the Developer, or each Tester) also gets
                    // their own block of rows to verify: person 1 = rows 1-20, person 2 = rows 21-40, ...
                    if (!_mode.IsProduction &&
                        _mode.AllocateUsernames.Count > 0 &&
                        !string.IsNullOrWhiteSpace(result.WeekTableName))
                    {
                        var parts = new List<string>();
                        var skip = 0;

                        foreach (var u in _mode.AllocateUsernames)
                        {
                            var n = await _cleaning.DevAllocateAsync(
                                result.WeekTableName!, u, _mode.AllocateCount, skip, skip == 0, ct);

                            parts.Add($"{u}: {n}");
                            skip += _mode.AllocateCount;
                        }

                        message += $" {_mode.Label.ToUpperInvariant()}: rows allocated for verifying ({string.Join(", ", parts)}).";
                    }

                    TempData["ExtractIntakeSuccess"] = message;
                }
            }
            catch (Exception ex)
            {
                TempData["ExtractIntakeError"] = "Cleaning failed: " + ex.Message;
            }

            return RedirectToAction(nameof(Clean), new { batchId });
        }

        // ═════════════════════════════════════════════════════════════════════
        // STEP 3 - email the managers
        // ═════════════════════════════════════════════════════════════════════

        [HttpGet("/ExtractIntake/Notify/{batchId:int}")]
        public async Task<IActionResult> Notify(int batchId, CancellationToken ct)
        {
            var vm = await BuildStepVmAsync(batchId, ct);
            if (vm is null)
                return NotFound();

            vm.ProjectManagerEmail = _emailCfg.ProjectManagerEmail;
            vm.QaManagerEmail = _emailCfg.NoticeQaManagerEmail;

            if (!string.IsNullOrWhiteSpace(vm.CleanWeekTableName) && vm.IsCleaned)
                vm.Summary = await TryGetSummaryAsync(vm.CleanWeekTableName!, ct);

            return View(vm);
        }

        [HttpPost("/ExtractIntake/Notify/{batchId:int}/Run")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NotifyRun(int batchId, CancellationToken ct)
        {
            if (!IsDataTeamUser())
                return Forbid();

            try
            {
                var batch = await _db.Batches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == batchId, ct);
                if (batch is null)
                    return NotFound();

                if (batch.ProjectManagerNotifiedAtUtc != null)
                {
                    TempData["ExtractIntakeSuccess"] = "The managers were already emailed for this extract.";
                }
                else
                {
                    await _approvalNotify.NotifyProjectManagerAfterCleanAsync(batchId, ct);
                    TempData["ExtractIntakeSuccess"] =
                        "Email sent to the Project Manager and QA. Sector Managers are emailed after the Project Manager approves.";
                }
            }
            catch (Exception ex)
            {
                TempData["ExtractIntakeError"] = "Email failed: " + ex.Message;
            }

            return RedirectToAction(nameof(Notify), new { batchId });
        }

        // ── progress (kept for scripts that poll it) ─────────────────────────

        [HttpGet("/ExtractIntake/Progress/{batchId:int}")]
        public async Task<IActionResult> Progress(int batchId, CancellationToken ct)
        {
            var batch = await _db.Batches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == batchId, ct);
            if (batch is null)
                return NotFound();

            var steps = await _db.CleaningStepAudits
                .AsNoTracking()
                .Where(x => x.BatchId == batchId)
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.StepName,
                    x.StartedAtUtc,
                    x.FinishedAtUtc,
                    x.DurationMs,
                    x.Success,
                    x.RowsAffected,
                    x.Message,
                    x.ErrorMessage
                })
                .ToListAsync(ct);

            return Json(new
            {
                batchId,
                batch.BatchName,
                batch.ExtractCleaningStatus,
                batch.ExtractCleaningMessage,
                steps
            });
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private async Task<WeekTableSummary?> TryGetSummaryAsync(string weekTable, CancellationToken ct)
        {
            try { return await _summary.GetAsync(weekTable, ct); }
            catch { return null; }
        }

        private async Task<ExtractStepVm?> BuildStepVmAsync(int batchId, CancellationToken ct)
        {
            var batch = await _db.Batches
                .AsNoTracking()
                .Include(b => b.WeekWindow)
                .FirstOrDefaultAsync(b => b.Id == batchId, ct);

            if (batch is null)
                return null;

            var file = await _db.ExtractFiles
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.BatchId == batchId, ct);

            var rawRows = await _db.ExtractRows
                .AsNoTracking()
                .CountAsync(x => x.BatchId == batchId, ct);

            return new ExtractStepVm
            {
                BatchId = batch.Id,
                BatchName = batch.BatchName,
                FileName = file?.OriginalFileName,
                WeekStart = batch.WeekWindow.WeekStart,
                WeekEnd = batch.WeekWindow.WeekEnd,
                NoticeSendDate = batch.WeekWindow.NoticeSendDate,
                ReviewOpenDate = batch.WeekWindow.ReviewOpenDate,
                ReviewCloseDate = batch.WeekWindow.ReviewCloseDate,
                RawRows = rawRows,
                CleanWeekTableName = batch.CleanWeekTableName,
                ExtractCleanedAtUtc = batch.ExtractCleanedAtUtc,
                ExtractCleanedBy = batch.ExtractCleanedBy,
                ExtractCleaningStatus = batch.ExtractCleaningStatus,
                ExtractCleaningMessage = batch.ExtractCleaningMessage,
                ProjectManagerNotifiedAtUtc = batch.ProjectManagerNotifiedAtUtc,
                ProjectManagerApprovedAtUtc = batch.ProjectManagerApprovedAtUtc,
                ProjectManagerApprovedBy = batch.ProjectManagerApprovedBy,
                ManagersNotifiedAtUtc = batch.ManagersNotifiedAtUtc,
                CanRun = IsDataTeamUser()
            };
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // View models
    // ═════════════════════════════════════════════════════════════════════════

    public sealed class ExtractIntakeIndexVm
    {
        public string FolderPath { get; set; } = "";
        public bool FolderExists { get; set; }

        public bool CanRunFolderImportNow { get; set; }

        public bool ImportIsRunning { get; set; }
        public DateTime? ImportStartedAtUtc { get; set; }
        public string? ImportStartedBy { get; set; }

        public List<ExtractFolderFileVm> FilesInFolder { get; set; } = new();
        public List<ExtractIntakeBatchVm> Batches { get; set; } = new();
        public List<ExtractFolderImportAuditVm> Audits { get; set; } = new();
    }

    public sealed class ExtractFolderFileVm
    {
        public string FileName { get; set; } = "";
        public string FullPath { get; set; } = "";
        public long SizeBytes { get; set; }
        public DateTime LastModified { get; set; }

        public bool Imported { get; set; }
        public int? BatchId { get; set; }
        public string? BatchName { get; set; }

        public string SizeDisplay =>
            SizeBytes >= 1024 * 1024
                ? $"{SizeBytes / 1024m / 1024m:N2} MB"
                : $"{SizeBytes / 1024m:N2} KB";
    }

    public sealed class ExtractIntakeBatchVm
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = "";
        public DateOnly BatchDate { get; set; }
        public string Status { get; set; } = "";

        public DateOnly WeekStart { get; set; }
        public DateOnly WeekEnd { get; set; }
        public DateOnly NoticeSendDate { get; set; }
        public DateOnly ReviewOpenDate { get; set; }
        public DateOnly ReviewCloseDate { get; set; }

        public string? OriginalFileName { get; set; }
        public long FileSizeBytes { get; set; }
        public DateTime UploadedAtUtc { get; set; }

        public int TotalRows { get; set; }

        public DateTime? ExtractCleanedAtUtc { get; set; }
        public string? ExtractCleanedBy { get; set; }
        public string? CleanWeekTableName { get; set; }
        public string? ExtractCleaningStatus { get; set; }
        public string? ExtractCleaningMessage { get; set; }

        public DateTime? ProjectManagerNotifiedAtUtc { get; set; }
        public DateTime? ProjectManagerApprovedAtUtc { get; set; }
        public DateTime? ManagersNotifiedAtUtc { get; set; }

        public bool IsCleaned =>
            ExtractCleanedAtUtc != null &&
            !string.IsNullOrWhiteSpace(CleanWeekTableName) &&
            !string.Equals(ExtractCleaningStatus, "Failed", StringComparison.OrdinalIgnoreCase);

        /// <summary>Which step the Data Team does next for this extract.</summary>
        public int NextStep =>
            !IsCleaned ? 2 :
            ProjectManagerNotifiedAtUtc == null ? 3 :
            4;

        public string NextStepText =>
            string.Equals(ExtractCleaningStatus, "Failed", StringComparison.OrdinalIgnoreCase)
                ? "Cleaning failed. Open Step 2 to try again."
            : !IsCleaned
                ? "Imported. Next: Step 2, clean it."
            : ProjectManagerNotifiedAtUtc == null
                ? "Cleaned. Next: Step 3, email the managers."
            : ManagersNotifiedAtUtc == null
                ? "Waiting for the Project Manager to approve."
            : "Managers emailed. Valuers are verifying.";

        public string FileSizeDisplay =>
            FileSizeBytes <= 0
                ? "-"
                : FileSizeBytes >= 1024 * 1024
                    ? $"{FileSizeBytes / 1024m / 1024m:N2} MB"
                    : $"{FileSizeBytes / 1024m:N2} KB";
    }

    public sealed class ExtractStepVm
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = "";
        public string? FileName { get; set; }

        public DateOnly WeekStart { get; set; }
        public DateOnly WeekEnd { get; set; }
        public DateOnly NoticeSendDate { get; set; }
        public DateOnly ReviewOpenDate { get; set; }
        public DateOnly ReviewCloseDate { get; set; }

        public int RawRows { get; set; }

        public string? CleanWeekTableName { get; set; }
        public DateTime? ExtractCleanedAtUtc { get; set; }
        public string? ExtractCleanedBy { get; set; }
        public string? ExtractCleaningStatus { get; set; }
        public string? ExtractCleaningMessage { get; set; }

        public DateTime? ProjectManagerNotifiedAtUtc { get; set; }
        public DateTime? ProjectManagerApprovedAtUtc { get; set; }
        public string? ProjectManagerApprovedBy { get; set; }
        public DateTime? ManagersNotifiedAtUtc { get; set; }

        public string? ProjectManagerEmail { get; set; }
        public string? QaManagerEmail { get; set; }

        public bool CanRun { get; set; }

        public List<ExtractStepAuditVm> Steps { get; set; } = new();
        public WeekTableSummary? Summary { get; set; }

        public bool Failed =>
            string.Equals(ExtractCleaningStatus, "Failed", StringComparison.OrdinalIgnoreCase);

        public bool IsCleaned =>
            ExtractCleanedAtUtc != null &&
            !string.IsNullOrWhiteSpace(CleanWeekTableName) &&
            !Failed;
    }

    public sealed class ExtractStepAuditVm
    {
        public string StepName { get; set; } = "";
        public DateTime? FinishedAtUtc { get; set; }
        public long? DurationMs { get; set; }
        public bool Success { get; set; }
        public int RowsAffected { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
