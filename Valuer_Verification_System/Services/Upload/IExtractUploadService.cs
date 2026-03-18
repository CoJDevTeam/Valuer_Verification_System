using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Infrastructure.Configuration;
using Valuer_Verification_System.Infrastructure.Configuration.Email;
using Valuer_Verification_System.Infrastructure.Storage;
using Valuer_Verification_System.Models.Data;

namespace Valuer_Verification_System.Services.Upload
{
    public interface IExtractUploadService
    {
        Task<int> UploadAsync(int weekWindowId, IFormFile file, string uploadedBy, CancellationToken ct);
        Task NotifyManagersAsync(int batchId, string notifiedBy, CancellationToken ct);
    }

    public sealed class ExtractUploadService : IExtractUploadService
    {
        private readonly VvsDbContext _db;
        private readonly IStorageService _storage;
        private readonly IEmailService _email;

        public ExtractUploadService(VvsDbContext db, IStorageService storage, IEmailService email)
        {
            _db = db;
            _storage = storage;
            _email = email;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Upload extract XLS + create batch + assignments
        // ─────────────────────────────────────────────────────────────────────
        public async Task<int> UploadAsync(int weekWindowId, IFormFile file, string uploadedBy, CancellationToken ct)
        {
            if (file is null || file.Length <= 0)
                throw new ArgumentException("Please select a valid Excel file.");

            var week = await _db.WeekWindows.FirstOrDefaultAsync(x => x.Id == weekWindowId, ct);
            if (week is null)
                throw new InvalidOperationException("Week window not found.");

            _storage.EnsureRootStructure();

            // 1) Save file to Extracts folder (keep original name)
            var savedExtractPath = await _storage.SaveExtractAsync(file, ct);

            // 2) Create Batch record
            var batch = new Batch
            {
                WeekWindowId = week.Id,
                BatchDate = DateOnly.FromDateTime(DateTime.Today),
                Status = BatchStatus.Uploaded,
                CreatedBy = uploadedBy,
                CreatedAt = DateTime.UtcNow
            };
            _db.Batches.Add(batch);
            await _db.SaveChangesAsync(ct);

            batch.BatchName = $"VVS_{batch.Id:0000}";
            await _db.SaveChangesAsync(ct);

            // 3) Save ExtractFile record
            var extractFile = new ExtractFile
            {
                BatchId = batch.Id,
                OriginalFileName = Path.GetFileName(file.FileName),
                SavedPath = savedExtractPath,
                FileSizeBytes = file.Length,
                UploadedAtUtc = DateTime.UtcNow,
                CreatedBy = uploadedBy,
                CreatedAt = DateTime.UtcNow
            };
            _db.ExtractFiles.Add(extractFile);
            await _db.SaveChangesAsync(ct);

            // 4) Parse Excel → ExtractRow list
            List<ExtractRow> rows;
            await using (var stream = file.OpenReadStream())
            {
                rows = ExcelExtractParser.ParseExtractRows(stream);
            }

            if (rows.Count == 0)
                throw new InvalidOperationException("No valid rows found in the extract.");

            // Attach batch + audit fields
            foreach (var r in rows)
            {
                r.BatchId = batch.Id;
                r.Decision = VerificationDecision.Pending;
                r.DecisionAtUtc = null;
                r.DecisionComment = null;
                r.CreatedBy = uploadedBy;
                r.CreatedAt = DateTime.UtcNow;
            }

            // 5) Bulk insert rows
            _db.ExtractRows.AddRange(rows);
            await _db.SaveChangesAsync(ct);

            // 6) Create valuer assignments from VALUER column
            // For split properties (same ValuationKey, SplitInd = P/Y),
            // assign only the PARENT row (SplitInd = 'P') or the first row
            // per ValuationKey — one assignment per property, not per split line.
            var assignments = rows
                .Where(r => !string.IsNullOrWhiteSpace(r.Valuer))
                .GroupBy(r => r.ValuationKey)
                .Select(g =>
                    // Prefer the parent row (SplitInd = 'P' or null/empty), else first
                    g.FirstOrDefault(r =>
                        string.IsNullOrWhiteSpace(r.ValuationSplitInd) ||
                        r.ValuationSplitInd.Trim().Equals("P", StringComparison.OrdinalIgnoreCase))
                    ?? g.First())
                .Where(r => r.Id > 0)   // must be saved (has PK)
                .Select(r => new Assignment
                {
                    ExtractRowId = r.Id,
                    AssignedToValuer = r.Valuer!.Trim(),
                    AssignedBy = uploadedBy,
                    AssignedAtUtc = DateTime.UtcNow,
                    CreatedBy = uploadedBy,
                    CreatedAt = DateTime.UtcNow
                })
                .ToList();

            _db.Assignments.AddRange(assignments);
            await _db.SaveChangesAsync(ct);

            // 7) Update week + batch statuses
            week.Status = WeekWindowStatus.Uploaded;
            batch.Status = BatchStatus.Uploaded;
            await _db.SaveChangesAsync(ct);

            return batch.Id;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Notify all active sector managers that a batch is ready for review
        // ─────────────────────────────────────────────────────────────────────
        public async Task NotifyManagersAsync(int batchId, string notifiedBy, CancellationToken ct)
        {
            var batch = await _db.Batches
                .Include(b => b.WeekWindow)
                .FirstOrDefaultAsync(b => b.Id == batchId, ct);

            if (batch is null)
                throw new InvalidOperationException("Batch not found.");

            if (batch.ManagersNotifiedAtUtc != null)
                throw new InvalidOperationException("Managers have already been notified for this batch.");

            // Extract file metadata (for archive path)
            var extract = await _db.ExtractFiles
                .AsNoTracking()
                .FirstAsync(x => x.BatchId == batchId, ct);

            // Summary counts for email
            var totalRecords = await _db.ExtractRows.CountAsync(r => r.BatchId == batchId, ct);
            var assignedRecords = await _db.Assignments.CountAsync(a => a.ExtractRow.BatchId == batchId, ct);

            // Fetch all active manager emails from SectorManagers table
            var managerEmails = await _db.SectorManagers
                .AsNoTracking()
                .Where(m => m.IsActive)
                .Select(m => m.Email)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e!.Trim())
                .Distinct()
                .ToListAsync(ct);

            if (managerEmails.Count == 0)
                throw new InvalidOperationException("No active sector manager email addresses found in the database.");

            // Use absolute URL path for the dashboard link
            var managerDashboardUrl = $"/Manager/Dashboard?batchId={batch.Id}";

            // Build email using the branded template
            var (subject, html, text) = EmailTemplates.ManagersUploadReady(
                batchName: batch.BatchName,
                batchDate: batch.BatchDate,
                weekStart: batch.WeekWindow.WeekStart,
                weekEnd: batch.WeekWindow.WeekEnd,
                noticeSendDate: batch.WeekWindow.NoticeSendDate,
                totalRecords: totalRecords,
                assignedRecords: assignedRecords,
                managerDashboardUrl: managerDashboardUrl
            );

            var req = new EmailRequest
            {
                To = managerEmails,
                Subject = subject,
                BodyHtml = html,
                BodyText = text
            };

            // Archive the .eml next to the extract file
            var baseEmlName = Path.GetFileNameWithoutExtension(extract.OriginalFileName);
            var emlPath = Path.Combine(
                Path.GetDirectoryName(_storage.GetExtractFilePath(extract.OriginalFileName))!,
                $"{baseEmlName}_managers.eml"
            );

            // Send + archive (never throw; always record outcome)
            EmailSendResult result;
            try
            {
                result = await _email.SendAndArchiveAsync(req, emlPath, ct);
            }
            catch (Exception ex)
            {
                result = new EmailSendResult { Sent = false, Error = ex.Message, ArchivePath = emlPath };
            }

            _db.EmailArchives.Add(new EmailArchive
            {
                BatchId = batch.Id,
                Kind = EmailKind.UploadReadyToManager,
                ToAddress = string.Join("; ", managerEmails),
                FromAddress = "",   // filled from EmailSettings by EmailService
                Subject = subject,
                EmlPath = result.ArchivePath,
                SentAtUtc = DateTime.UtcNow,
                ErrorMessage = result.Sent ? null : result.Error,
                CreatedBy = notifiedBy,
                CreatedAt = DateTime.UtcNow
            });

            batch.ManagersNotifiedAtUtc = DateTime.UtcNow;
            batch.ManagersNotifiedBy = notifiedBy;
            batch.Status = BatchStatus.InVerification;

            await _db.SaveChangesAsync(ct);

            // Propagate send failure so the UI can warn the user (upload is already saved)
            if (!result.Sent && !string.IsNullOrWhiteSpace(result.Error))
                throw new InvalidOperationException(
                    $"Batch marked as notified but email failed to send: {result.Error}");
        }
    }
}