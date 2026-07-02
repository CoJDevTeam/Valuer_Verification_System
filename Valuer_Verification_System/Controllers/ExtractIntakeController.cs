using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Infrastructure.Configuration;
using Valuer_Verification_System.Models.Data;
using Valuer_Verification_System.Models.ViewModels;
using Valuer_Verification_System.Services.Notice;
using Valuer_Verification_System.Services.Upload;

namespace Valuer_Verification_System.Controllers
{
    [Authorize]
    public sealed class ExtractIntakeController : Controller
    {
        private readonly VvsDbContext _db;
        private readonly IConfiguration _config;
        private readonly IExtractFolderImportService _folderImport;
        private readonly IExtractFolderImportLock _folderImportLock;
        private readonly INoticeCleaningService _cleaning;
        private readonly ExtractInboxSettings _settings;
        public ExtractIntakeController(
            VvsDbContext db,
            IConfiguration config,
            IExtractFolderImportService folderImport,IExtractFolderImportLock folderImportLock,INoticeCleaningService cleaningService
            , Microsoft.Extensions.Options.IOptions<ExtractInboxSettings> settings)
        {
            _db = db;
            _config = config;
            _folderImport = folderImport;
            _folderImportLock = folderImportLock;
            _settings = settings.Value;
            _cleaning = cleaningService;
        }

        //        private bool CanRunFolderImportNow()
        //{
        //    var role = User.FindFirst("Role")?.Value
        //               ?? User.FindFirst("Position")?.Value
        //               ?? User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
        //               ?? "";

        //    var userName = User.Identity?.Name ?? "";

        //    return role.Equals("Admin", StringComparison.OrdinalIgnoreCase)
        //        || role.Equals("Syscon", StringComparison.OrdinalIgnoreCase)
        //        || role.Contains("DEPUTY DIRECTOR", StringComparison.OrdinalIgnoreCase)
        //        || userName.Contains("Syscon", StringComparison.OrdinalIgnoreCase);
        //}

        private bool CanRunFolderImportNow()
        {
            if (!_settings.AllowManualRun)
                return false;

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

        [HttpGet("/ExtractIntake")]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var folderPath = _config["ExtractInbox:FolderPath"] ?? "";

            var filesInFolder = new List<ExtractFolderFileVm>();

            if (!string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
            {
                filesInFolder = Directory.GetFiles(folderPath)
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
                    .Take(20)
                    .ToList();
            }

            var batches = await _db.Batches
                .AsNoTracking()
                .Include(b => b.WeekWindow)
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
                    ProjectManagerNotifiedTo = b.ProjectManagerNotifiedTo,
                    QaNotifiedTo = b.QaNotifiedTo,

                    ProjectManagerApprovedAtUtc = b.ProjectManagerApprovedAtUtc,
                    ProjectManagerApprovedBy = b.ProjectManagerApprovedBy,
                    ProjectManagerApprovalComment = b.ProjectManagerApprovalComment,

                    ManagersNotifiedAtUtc = b.ManagersNotifiedAtUtc,
                    ManagersNotifiedBy = b.ManagersNotifiedBy
                })
                .ToListAsync(ct);

            var batchIds = batches.Select(x => x.BatchId).ToList();

            var fileLogs = await _db.ExtractFiles
                .AsNoTracking()
                .Where(x => batchIds.Contains(x.BatchId))
                .Select(x => new
                {
                    x.BatchId,
                    x.OriginalFileName,
                    x.SavedPath,
                    x.FileSizeBytes,
                    x.UploadedAtUtc,
                    x.FileHashSha256
                })
                .ToListAsync(ct);

            var rowStats = await _db.ExtractRows
                .AsNoTracking()
                .Where(x => batchIds.Contains(x.BatchId))
                .GroupBy(x => x.BatchId)
                .Select(g => new
                {
                    BatchId = g.Key,
                    TotalRows = g.Count(),
                    TotalProperties = g.Select(x => x.ValuationKey).Distinct().Count(),
                    Pending = g.Count(x => x.Decision == VerificationDecision.Pending),
                    Yes = g.Count(x => x.Decision == VerificationDecision.Yes),
                    No = g.Count(x => x.Decision == VerificationDecision.No),
                    MatchedValuers = g.Where(x => x.Username != null && x.Username != "")
                                      .Select(x => x.Username)
                                      .Distinct()
                                      .Count(),
                    UnmatchedRows = g.Count(x => x.Username == null || x.Username == ""),
                    Sectors = g.Select(x => x.Sector).Distinct().Count()
                })
                .ToListAsync(ct);

            var sectorStats = await _db.ExtractRows
                .AsNoTracking()
                .Where(x => batchIds.Contains(x.BatchId))
                .GroupBy(x => new
                {
                    x.BatchId,
                    Sector = string.IsNullOrWhiteSpace(x.Sector) ? "Unassigned" : x.Sector!.Trim()
                })
                .Select(g => new ExtractIntakeSectorVm
                {
                    BatchId = g.Key.BatchId,
                    Sector = g.Key.Sector,
                    TotalRows = g.Count(),
                    TotalProperties = g.Select(x => x.ValuationKey).Distinct().Count(),
                    Pending = g.Count(x => x.Decision == VerificationDecision.Pending),
                    Yes = g.Count(x => x.Decision == VerificationDecision.Yes),
                    No = g.Count(x => x.Decision == VerificationDecision.No),
                    UnmatchedRows = g.Count(x => x.Username == null || x.Username == "")
                })
                .OrderBy(x => x.BatchId)
                .ThenBy(x => x.Sector)
                .ToListAsync(ct);

            foreach (var b in batches)
            {
                var file = fileLogs.FirstOrDefault(x => x.BatchId == b.BatchId);
                if (file is not null)
                {
                    b.OriginalFileName = file.OriginalFileName;
                    b.SavedPath = file.SavedPath;
                    b.FileSizeBytes = file.FileSizeBytes;
                    b.UploadedAtUtc = file.UploadedAtUtc;
                    b.FileHashSha256 = file.FileHashSha256;
                }

                var stats = rowStats.FirstOrDefault(x => x.BatchId == b.BatchId);
                if (stats is not null)
                {
                    b.TotalRows = stats.TotalRows;
                    b.TotalProperties = stats.TotalProperties;
                    b.Pending = stats.Pending;
                    b.Yes = stats.Yes;
                    b.No = stats.No;
                    b.MatchedValuers = stats.MatchedValuers;
                    b.UnmatchedRows = stats.UnmatchedRows;
                    b.SectorsCount = stats.Sectors;
                }

                b.Sectors = sectorStats
                    .Where(x => x.BatchId == b.BatchId)
                    .ToList();
            }

            var audits = await _db.ExtractFolderImportAudits
    .AsNoTracking()
    .OrderByDescending(x => x.StartedAtUtc)
    .Take(20)
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
            var vm = new ExtractIntakeIndexVm
            {
                FolderPath = folderPath,
                FolderExists = !string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath),
                CanRunFolderImportNow = CanRunFolderImportNow(),

                ImportIsRunning = _folderImportLock.IsRunning,
                ImportStartedAtUtc = _folderImportLock.StartedAtUtc,
                ImportStartedBy = _folderImportLock.StartedBy,

                FilesInFolder = filesInFolder,
                Batches = batches
            };

            return View(vm);
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
                        "Folder import is already running. Please wait for the current import to finish.";
                }
                else
                {
                    TempData["ExtractIntakeSuccess"] =
                        $"Folder import completed. Imported: {result.ImportedCount}, Skipped: {result.SkippedCount}, Failed: {result.FailedCount}.";
                }
            }
            catch (Exception ex)
            {
                TempData["ExtractIntakeError"] =
                    "Folder import failed: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
        [HttpGet("/ExtractIntake/Progress/{batchId:int}")]
        public async Task<IActionResult> Progress(int batchId, CancellationToken ct)
        {
            var batch = await _db.Batches
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == batchId, ct);

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

            var totalSteps = _cleaning.StepNames.Count + 1; // + CreateWeekTable
            var completedSteps = steps.Count(x => x.FinishedAtUtc != null);

            var percent = totalSteps <= 0
                ? 0
                : Math.Round((decimal)completedSteps * 100m / totalSteps, 2);

            return Json(new
            {
                batchId,
                batch.BatchName,
                batch.ExtractCleaningStatus,
                batch.ExtractCleaningMessage,
                percent,
                completedSteps,
                totalSteps,
                steps
            });
        }
    }


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
        public string? SavedPath { get; set; }
        public long FileSizeBytes { get; set; }
        public DateTime UploadedAtUtc { get; set; }
        public string? FileHashSha256 { get; set; }

        public int TotalRows { get; set; }
        public int TotalProperties { get; set; }
        public int Pending { get; set; }
        public int Yes { get; set; }
        public int No { get; set; }
        public int MatchedValuers { get; set; }
        public int UnmatchedRows { get; set; }
        public int SectorsCount { get; set; }

        public DateTime? ExtractCleanedAtUtc { get; set; }
        public string? ExtractCleanedBy { get; set; }
        public string? CleanWeekTableName { get; set; }
        public string? ExtractCleaningStatus { get; set; }
        public string? ExtractCleaningMessage { get; set; }

        public DateTime? ProjectManagerNotifiedAtUtc { get; set; }
        public string? ProjectManagerNotifiedTo { get; set; }
        public string? QaNotifiedTo { get; set; }

        public DateTime? ProjectManagerApprovedAtUtc { get; set; }
        public string? ProjectManagerApprovedBy { get; set; }
        public string? ProjectManagerApprovalComment { get; set; }

        public DateTime? ManagersNotifiedAtUtc { get; set; }
        public string? ManagersNotifiedBy { get; set; }

        public List<ExtractIntakeSectorVm> Sectors { get; set; } = new();

        public int Verified => Yes + No;

        public decimal ProgressPercent =>
            TotalRows <= 0 ? 0 : Math.Round((decimal)Verified * 100m / TotalRows, 2);

        public string FileSizeDisplay =>
            FileSizeBytes <= 0
                ? "-"
                : FileSizeBytes >= 1024 * 1024
                    ? $"{FileSizeBytes / 1024m / 1024m:N2} MB"
                    : $"{FileSizeBytes / 1024m:N2} KB";
    }

    public sealed class ExtractIntakeSectorVm
    {
        public int BatchId { get; set; }
        public string Sector { get; set; } = "";
        public int TotalRows { get; set; }
        public int TotalProperties { get; set; }
        public int Pending { get; set; }
        public int Yes { get; set; }
        public int No { get; set; }
        public int UnmatchedRows { get; set; }

        public int Verified => Yes + No;

        public decimal ProgressPercent =>
            TotalRows <= 0 ? 0 : Math.Round((decimal)Verified * 100m / TotalRows, 2);
    }
}