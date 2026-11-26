using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Domain.Dates;
using Valuer_Verification_System.Infrastructure.Configuration;
using Valuer_Verification_System.Models.Data;
using Valuer_Verification_System.Services.Notice;

namespace Valuer_Verification_System.Services.Upload
{
    public interface IExtractFolderImportService
    {
        Task<ExtractFolderImportResult> ImportNewFilesAsync(
    string runType,
    string startedBy,
    CancellationToken ct);
    }

    public sealed class ExtractFolderImportResult
    {
        public int ImportedCount { get; set; }
        public int SkippedCount { get; set; }
        public int FailedCount { get; set; }
        public List<string> Messages { get; set; } = new();
    }

    public sealed class ExtractFolderImportService : IExtractFolderImportService
    {
        private readonly VvsDbContext _db;
        private readonly IValuerUsernameResolver _resolver;
        private readonly IWeekWindowCalculator _weekCalc;
        private readonly INoticeCleaningService _cleaning;
        private readonly ILatestCleaningTableResolver _latestTables;
        private readonly IExtractApprovalNotificationService _approvalNotify;
        private readonly ExtractInboxSettings _settings;
        private readonly ILogger<ExtractFolderImportService> _logger;
        private readonly IExtractFolderImportLock _importLock;
        public ExtractFolderImportService(
            VvsDbContext db,
            IValuerUsernameResolver resolver,
            IWeekWindowCalculator weekCalc,
            INoticeCleaningService cleaning,
            ILatestCleaningTableResolver latestTables,
            IExtractApprovalNotificationService approvalNotify,
            Microsoft.Extensions.Options.IOptions<ExtractInboxSettings> settings,
            ILogger<ExtractFolderImportService> logger, IExtractFolderImportLock importLock)
        {
            _db = db;
            _resolver = resolver;
            _weekCalc = weekCalc;
            _cleaning = cleaning;
            _latestTables = latestTables;
            _approvalNotify = approvalNotify;
            _settings = settings.Value;
            _logger = logger;
            _importLock = importLock;
        }

        public async Task<ExtractFolderImportResult> ImportNewFilesAsync(
     string runType,
     string startedBy,
     CancellationToken ct)
        {
            runType = string.IsNullOrWhiteSpace(runType) ? "Unknown" : runType.Trim();
            startedBy = string.IsNullOrWhiteSpace(startedBy) ? "system" : startedBy.Trim();

            var audit = new ExtractFolderImportAudit
            {
                RunType = runType,
                StartedBy = startedBy,
                StartedAtUtc = DateTime.UtcNow,
                Success = false,
                FolderPath = _settings.FolderPath,
                MachineName = Environment.MachineName,
                CreatedAt = DateTime.UtcNow
            };

            _db.ExtractFolderImportAudits.Add(audit);
            await _db.SaveChangesAsync(ct);

            await using var lockHandle = await _importLock.TryAcquireAsync(ct);

            if (lockHandle is null)
            {
                audit.FinishedAtUtc = DateTime.UtcNow;
                audit.Success = false;
                audit.ImportedCount = 0;
                audit.SkippedCount = 1;
                audit.FailedCount = 0;
                audit.Message = "Folder import skipped because another folder import is already running.";
                await _db.SaveChangesAsync(ct);

                _logger.LogWarning("Folder import skipped because another folder import is already running.");

                return new ExtractFolderImportResult
                {
                    ImportedCount = 0,
                    SkippedCount = 1,
                    FailedCount = 0,
                    Messages =
            {
                "Folder import skipped because another folder import is already running."
            }
                };
            }

            var result = new ExtractFolderImportResult();

            try
            {
                EnsureFolder();

                var regex = new Regex(
                    _settings.FileNamePattern,
                    RegexOptions.IgnoreCase | RegexOptions.Compiled);

                var files = Directory.GetFiles(_settings.FolderPath)
                    .Where(path => regex.IsMatch(Path.GetFileName(path)))
                    .OrderBy(path => path)
                    .ToList();

                foreach (var filePath in files)
                {
                    try
                    {
                        var message = await ImportOneFileAsync(filePath, regex, ct);

                        result.Messages.Add($"{Path.GetFileName(filePath)}: {message}");

                        if (message.StartsWith("Imported.", StringComparison.OrdinalIgnoreCase))
                            result.ImportedCount++;
                        else
                            result.SkippedCount++;

                        _logger.LogInformation(
                            "Folder import result for {File}: {Message}",
                            filePath,
                            message);
                    }
                    catch (Exception ex)
                    {
                        result.FailedCount++;

                        var msg = $"{Path.GetFileName(filePath)} failed: {ex.Message}";
                        result.Messages.Add(msg);

                        _logger.LogError(ex, "Folder import failed for {FilePath}", filePath);

                        WriteFailureLog(filePath, ex);
                    }
                }

                audit.FinishedAtUtc = DateTime.UtcNow;
                audit.ImportedCount = result.ImportedCount;
                audit.SkippedCount = result.SkippedCount;
                audit.FailedCount = result.FailedCount;
                audit.Success = result.FailedCount == 0;
                audit.Message = result.Messages.Count == 0
                    ? "No matching extract files found."
                    : string.Join(" | ", result.Messages.Take(10));

                await _db.SaveChangesAsync(ct);

                return result;
            }
            catch (Exception ex)
            {
                audit.FinishedAtUtc = DateTime.UtcNow;
                audit.Success = false;
                audit.ImportedCount = result.ImportedCount;
                audit.SkippedCount = result.SkippedCount;
                audit.FailedCount = result.FailedCount;
                audit.ErrorMessage = ex.ToString();
                audit.Message = ex.Message;

                await _db.SaveChangesAsync(ct);

                throw;
            }
        }

        private async Task<string> ImportOneFileAsync(
         string sourcePath,
         Regex regex,
         CancellationToken ct)
        {
            var fileName = Path.GetFileName(sourcePath);
            var match = regex.Match(fileName);

            if (!match.Success)
                return "Skipped invalid file name.";

            var weekStart = DateOnly.ParseExact(
                match.Groups[1].Value,
                "yyyyMMdd",
                CultureInfo.InvariantCulture);

            var weekEnd = DateOnly.ParseExact(
                match.Groups[2].Value,
                "yyyyMMdd",
                CultureInfo.InvariantCulture);

            ValidateWeekDates(weekStart, weekEnd);

            var hash = await ComputeSha256Async(sourcePath, ct);
            var fileSize = new FileInfo(sourcePath).Length;

            var existingFile = await _db.ExtractFiles
                .Include(x => x.Batch)
                .FirstOrDefaultAsync(x =>
                    x.OriginalFileName == fileName &&
                    x.FileHashSha256 == hash,
                    ct);

            if (existingFile is not null)
            {
                // A previous run saved the batch and raw rows but the week table / import
                // cleaning did not finish. Retry that part instead of skipping forever.
                var unfinished = existingFile.Batch;

                if (_settings.AutoCleanAfterImport && unfinished.ExtractCleanedAtUtc == null)
                {
                    var rawCount = await _db.ExtractRows.CountAsync(r => r.BatchId == unfinished.Id, ct);
                    var msg = await FinishWeekTableAsync(unfinished, ct);
                    return $"Imported. Batch {unfinished.BatchName}. Rows {rawCount}. (retry) {msg}";
                }

                return "Skipped. Same file name and same hash already imported.";
            }

            var sameFileDifferentHash = await _db.ExtractFiles
                .AsNoTracking()
                .AnyAsync(x =>
                    x.OriginalFileName == fileName &&
                    x.FileHashSha256 != hash,
                    ct);

            if (sameFileDifferentHash)
            {
                throw new InvalidOperationException(
                    "A file with the same name was already imported but the content/hash is different. Import blocked to protect audit history.");
            }

            var week = await GetOrCreateWeekWindowAsync(weekStart, weekEnd, ct);

            var batch = new Batch
            {
                WeekWindowId = week.Id,
                BatchDate = weekStart,
                Status = BatchStatus.Uploaded,
                CreatedBy = "folder-job",
                CreatedAt = DateTime.UtcNow
            };

            _db.Batches.Add(batch);
            await _db.SaveChangesAsync(ct);

            batch.BatchName = $"VVS_{batch.Id:0000}";
            await _db.SaveChangesAsync(ct);

            var extractFile = new ExtractFile
            {
                BatchId = batch.Id,
                OriginalFileName = fileName,
                SavedPath = sourcePath,
                FileSizeBytes = fileSize,
                UploadedAtUtc = DateTime.UtcNow,
                FileHashSha256 = hash,
                CreatedBy = "folder-job",
                CreatedAt = DateTime.UtcNow
            };

            _db.ExtractFiles.Add(extractFile);
            await _db.SaveChangesAsync(ct);

            var rows = ExcelExtractParser.ParseExtractRowsFromFile(sourcePath);

            if (rows.Count == 0)
                throw new InvalidOperationException("No valid rows found in the extract.");

            var rowNo = 1;

            foreach (var row in rows)
            {
                row.BatchId = batch.Id;

                row.Decision = VerificationDecision.Pending;
                row.DecisionAtUtc = null;
                row.DecisionComment = null;

                row.SourceFileName = fileName;
                row.SourceFileHashSha256 = hash;
                row.SourceRowNumber = rowNo++;

                row.CreatedBy = "folder-job";
                row.CreatedAt = DateTime.UtcNow;

                // Do NOT resolve username here.
                // Valuer may be populated later by sp_VVS_Clean_SectorValuer.
                row.ResolvedUserId = null;
                row.Username = null;
                row.ResolvedValuerEmail = null;
                row.ValuerMatchStatus = null;
                row.ValuerMatchComment = null;
            }

            _db.ExtractRows.AddRange(rows);
            await _db.SaveChangesAsync(ct);

            var cleaningMessage = await FinishWeekTableAsync(batch, ct);

            return $"Imported. Batch {batch.BatchName}. Rows {rows.Count}. {cleaningMessage}";
        }

        // Phase 1: ExtractRows stays as the untouched raw backup.
        // The WEEK TABLE is the working table: it gets ALL raw rows, plus the import
        // steps (sector, valuer, trim). Valuers verify in it. Safe to call again for a
        // batch whose first attempt failed.
        private async Task<string> FinishWeekTableAsync(Batch batch, CancellationToken ct)
        {
            var week = await _db.WeekWindows.FirstAsync(w => w.Id == batch.WeekWindowId, ct);
            var cleaningMessage = "Week table not created (AutoCleanAfterImport is off).";
            var cleaningDone = false;

            if (_settings.AutoCleanAfterImport)
            {
                var latestTables = await _latestTables.ResolveAsync(ct);

                var imp = await _cleaning.ImportWeekTableAndCleanAsync(
                    batch.Id, "folder-job", latestTables.LisTable, latestTables.SapTable, ct);

                if (!imp.Success)
                    throw new InvalidOperationException(imp.Message);

                cleaningDone = true;
                cleaningMessage = imp.Message;

                // DEV/TEST ONLY: give the first N rows to the developer so there is always
                // something to verify. Username on the rows is NOT changed.
                if (_settings.DevAllocateEnabled &&
                    !string.IsNullOrWhiteSpace(_settings.DevAllocateUsername) &&
                    !string.IsNullOrWhiteSpace(imp.WeekTableName))
                {
                    var devRows = await _cleaning.DevAllocateAsync(
                        imp.WeekTableName!, _settings.DevAllocateUsername, _settings.DevAllocateCount, ct);
                    cleaningMessage += $" DEV: {devRows} rows allocated to {_settings.DevAllocateUsername}.";
                }
            }

            week.Status = WeekWindowStatus.Uploaded;
            week.UpdatedBy = "folder-job";
            week.UpdatedAt = DateTime.UtcNow;

            batch.Status = BatchStatus.Uploaded;
            batch.UpdatedBy = "folder-job";
            batch.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);

            if (cleaningDone)
            {
                // After cleaning and username resolving, notify QA + Project Manager only.
                // Sector Managers are notified only after Project Manager approval.
                await _approvalNotify.NotifyProjectManagerAfterCleanAsync(batch.Id, ct);
            }

            return cleaningMessage;
        }

        private async Task<int> ResolveBatchValuerUsernamesAsync(
      int batchId,
      CancellationToken ct)
        {
            var rows = await _db.ExtractRows
                .Where(r => r.BatchId == batchId)
                .ToListAsync(ct);

            var resolvedCount = 0;

            foreach (var row in rows)
            {
                var resolution = await _resolver.ResolveAsync(row.Valuer, ct);

                row.ResolvedUserId = resolution.UserId;
                row.Username = resolution.Username;
                row.ResolvedValuerEmail = resolution.EmailAddress;
                row.ValuerMatchStatus = resolution.Status;
                row.ValuerMatchComment = resolution.Comment;

                if (!string.IsNullOrWhiteSpace(row.Username))
                    resolvedCount++;
            }

            await _db.SaveChangesAsync(ct);

            return resolvedCount;
        }

        private async Task<int> CreateAssignmentsForBatchAsync(
    int batchId,
    CancellationToken ct)
        {
            var existingAssignments = await _db.Assignments
                .AsNoTracking()
                .Where(a => a.ExtractRow.BatchId == batchId)
                .ToListAsync(ct);

            if (existingAssignments.Count > 0)
            {
                _db.Assignments.RemoveRange(existingAssignments);
                await _db.SaveChangesAsync(ct);
            }

            // The SP ran via raw SQL and updated Username directly in the DB.
            // EF's change tracker still holds stale entities with Username = null.
            // Detach all tracked ExtractRow entities so the query below reads
            // fresh data from the DB rather than the stale tracked versions.
            var staleEntries = _db.ChangeTracker.Entries<ExtractRow>()
                .Where(e => e.Entity.BatchId == batchId)
                .ToList();

            foreach (var entry in staleEntries)
                entry.State = Microsoft.EntityFrameworkCore.EntityState.Detached;

            var rows = await _db.ExtractRows
                .AsNoTracking()
                .Where(r =>
                    r.BatchId == batchId &&
                    r.Username != null && r.Username != "")
                .ToListAsync(ct);

            var assignments = rows
                .GroupBy(r => r.ValuationKey)
                .Select(g =>
                    g.FirstOrDefault(r =>
                        string.IsNullOrWhiteSpace(r.ValuationSplitInd) ||
                        r.ValuationSplitInd.Trim().Equals("P", StringComparison.OrdinalIgnoreCase))
                    ?? g.First())
                .Where(r => r.Id > 0)
                .Select(r => new Assignment
                {
                    ExtractRowId = r.Id,
                    AssignedToValuer = (r.Username ?? "").Trim(),
                    AssignedBy = "folder-job",
                    AssignedAtUtc = DateTime.UtcNow,
                    CreatedBy = "folder-job",
                    CreatedAt = DateTime.UtcNow
                })
                .ToList();

            _db.Assignments.AddRange(assignments);
            await _db.SaveChangesAsync(ct);

            return assignments.Count;
        }
        private async Task<WeekWindow> GetOrCreateWeekWindowAsync(
            DateOnly weekStart,
            DateOnly weekEnd,
            CancellationToken ct)
        {
            var existing = await _db.WeekWindows
                .FirstOrDefaultAsync(x => x.WeekStart == weekStart, ct);

            if (existing is not null)
                return existing;

            var computedWeekEnd = _weekCalc.ComputeWeekEnd(weekStart);

            if (computedWeekEnd != weekEnd)
            {
                throw new InvalidOperationException(
                    $"File name week end date is invalid. Expected {computedWeekEnd:yyyyMMdd} from start date {weekStart:yyyyMMdd}, but file has {weekEnd:yyyyMMdd}.");
            }

            var noticeSendDate = _weekCalc.ComputeNoticeSendDate(computedWeekEnd);
            var reviewOpenDate = _weekCalc.ComputeReviewOpenDate(noticeSendDate);
            var reviewCloseDate = _weekCalc.ComputeReviewCloseDate(reviewOpenDate);

            var week = new WeekWindow
            {
                WeekStart = weekStart,
                WeekEnd = computedWeekEnd,
                NoticeSendDate = noticeSendDate,
                ReviewOpenDate = reviewOpenDate,
                ReviewCloseDate = reviewCloseDate,
                Status = WeekWindowStatus.Draft,
                Notes = "Auto-created from extract folder job.",
                CreatedBy = "folder-job",
                CreatedAt = DateTime.UtcNow
            };

            _db.WeekWindows.Add(week);
            await _db.SaveChangesAsync(ct);

            return week;
        }

        private void ValidateWeekDates(DateOnly weekStart, DateOnly weekEnd)
        {
            var computedWeekEnd = _weekCalc.ComputeWeekEnd(weekStart);

            if (computedWeekEnd != weekEnd)
            {
                throw new InvalidOperationException(
                    $"Invalid extract file dates. File has {weekStart:yyyyMMdd}_To_{weekEnd:yyyyMMdd}, but expected end date is {computedWeekEnd:yyyyMMdd}.");
            }
        }

        private void EnsureFolder()
        {
            if (string.IsNullOrWhiteSpace(_settings.FolderPath))
                throw new InvalidOperationException("ExtractInbox:FolderPath is missing in appsettings.");

            Directory.CreateDirectory(_settings.FolderPath);
        }

        private void WriteFailureLog(string sourcePath, Exception ex)
        {
            try
            {
                var errorPath = sourcePath + ".error.txt";

                File.WriteAllText(
                    errorPath,
                    $"Failed at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}{ex}");
            }
            catch
            {
                // Do not throw from failure handling.
            }
        }

        private static async Task<string> ComputeSha256Async(string path, CancellationToken ct)
        {
            await using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();

            var hashBytes = await sha.ComputeHashAsync(stream, ct);

            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}