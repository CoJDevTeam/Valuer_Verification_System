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
        Task<ExtractFolderImportResult> ImportNewFilesAsync(CancellationToken ct);
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

        public ExtractFolderImportService(
            VvsDbContext db,
            IValuerUsernameResolver resolver,
            IWeekWindowCalculator weekCalc,
            INoticeCleaningService cleaning,
            ILatestCleaningTableResolver latestTables,
            IExtractApprovalNotificationService approvalNotify,
            Microsoft.Extensions.Options.IOptions<ExtractInboxSettings> settings,
            ILogger<ExtractFolderImportService> logger)
        {
            _db = db;
            _resolver = resolver;
            _weekCalc = weekCalc;
            _cleaning = cleaning;
            _latestTables = latestTables;
            _approvalNotify = approvalNotify;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<ExtractFolderImportResult> ImportNewFilesAsync(CancellationToken ct)
        {
            var result = new ExtractFolderImportResult();

            EnsureFolder();

            var regex = new Regex(
                _settings.FileNamePattern,
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            var files = Directory.GetFiles(_settings.FolderPath)
                .Where(path => regex.IsMatch(Path.GetFileName(path)))
                .OrderBy(path => path)
                .ToList();

            if (files.Count == 0)
            {
                result.Messages.Add("No extract files found.");
                return result;
            }

            foreach (var filePath in files)
            {
                try
                {
                    var importResult = await ImportOneFileAsync(filePath, regex, ct);

                    if (importResult.StartsWith("Imported.", StringComparison.OrdinalIgnoreCase))
                        result.ImportedCount++;
                    else
                        result.SkippedCount++;

                    result.Messages.Add($"{Path.GetFileName(filePath)}: {importResult}");
                }
                catch (Exception ex)
                {
                    result.FailedCount++;

                    _logger.LogError(
                        ex,
                        "Failed to import extract file {FilePath}.",
                        filePath);

                    result.Messages.Add($"{Path.GetFileName(filePath)}: Failed - {ex.Message}");

                    // Do not move/delete the file.
                    // Leave it in the folder so the issue can be corrected and reprocessed.
                    WriteFailureLog(filePath, ex);
                }
            }

            return result;
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

            var sameHashExists = await _db.ExtractFiles
                .AsNoTracking()
                .AnyAsync(x =>
                    x.OriginalFileName == fileName &&
                    x.FileHashSha256 == hash,
                    ct);

            if (sameHashExists)
                return "Skipped. Same file name and same hash already imported.";

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

            // Important:
            // Folder job does not copy/move the extract like manual upload.
            // The file remains in the configured extract folder.
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

                await ResolveValuerUsernameAsync(row, ct);
            }

            _db.ExtractRows.AddRange(rows);
            await _db.SaveChangesAsync(ct);

            // Keep Assignments populated for old screens.
            // Later we will move Valuer Inbox fully to ExtractRows.Username.
            var assignments = rows
                .Where(r => !string.IsNullOrWhiteSpace(r.Username))
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
                    AssignedToValuer = r.Username!.Trim(),
                    AssignedBy = "folder-job",
                    AssignedAtUtc = DateTime.UtcNow,
                    CreatedBy = "folder-job",
                    CreatedAt = DateTime.UtcNow
                })
                .ToList();

            _db.Assignments.AddRange(assignments);

            week.Status = WeekWindowStatus.Uploaded;
            week.UpdatedBy = "folder-job";
            week.UpdatedAt = DateTime.UtcNow;

            batch.Status = BatchStatus.Uploaded;
            batch.UpdatedBy = "folder-job";
            batch.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);

            var cleaningMessage = "Auto-clean skipped.";

            if (_settings.AutoCleanAfterImport)
            {
                var latestTables = await _latestTables.ResolveAsync(ct);

                var cleanResult = await _cleaning.RunFullCleaningOnceAsync(
                    batch.Id,
                    weekStart,
                    latestTables.LisTable,
                    latestTables.SapTable,
                    ct);

                if (!cleanResult.Success)
                    throw new InvalidOperationException(cleanResult.Error);

                cleaningMessage =
                    $"Auto-clean completed once using {latestTables.LisTable} and {latestTables.SapTable}. " +
                    $"Rows affected {cleanResult.RowsAffected:N0}.";

                var freshBatch = await _db.Batches
                    .FirstAsync(x => x.Id == batch.Id, ct);

                freshBatch.ExtractCleaningMessage =
                    $"{freshBatch.ExtractCleaningMessage} | Cleaning tables used: LIS={latestTables.LisTable}, SAP={latestTables.SapTable}";

                freshBatch.UpdatedBy = "folder-job";
                freshBatch.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync(ct);

                // After cleaning, notify QA + Project Manager only.
                // Sector Managers are notified only after Project Manager approval.
                await _approvalNotify.NotifyProjectManagerAfterCleanAsync(batch.Id, ct);
            }

            return $"Imported. Batch {batch.BatchName}. Rows {rows.Count}. Assignments {assignments.Count}. {cleaningMessage}";
        }

        private async Task ResolveValuerUsernameAsync(ExtractRow row, CancellationToken ct)
        {
            var resolution = await _resolver.ResolveAsync(row.Valuer, ct);

            row.ResolvedUserId = resolution.UserId;
            row.Username = resolution.Username;
            row.ResolvedValuerEmail = resolution.EmailAddress;
            row.ValuerMatchStatus = resolution.Status;
            row.ValuerMatchComment = resolution.Comment;
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