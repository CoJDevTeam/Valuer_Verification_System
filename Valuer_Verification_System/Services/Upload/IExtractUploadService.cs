using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Infrastructure.Configuration;
using Valuer_Verification_System.Infrastructure.Configuration.Email;
using Valuer_Verification_System.Infrastructure.Configuration.Email.Valuer_Verification_System.Infrastructure.Configuration.Email;
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

        public async Task<int> UploadAsync(int weekWindowId, IFormFile file, string uploadedBy, CancellationToken ct)
        {
            if (file is null || file.Length <= 0)
                throw new ArgumentException("Please select a valid Excel file.");

            var week = await _db.WeekWindows.FirstOrDefaultAsync(x => x.Id == weekWindowId, ct);
            if (week is null)
                throw new InvalidOperationException("Week window not found.");

            _storage.EnsureRootStructure();

            // 1) Save file (Extracts folder) - keep original name
            var savedExtractPath = await _storage.SaveExtractAsync(file, ct);

            // 2) Create Batch
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

            // 4) Parse Excel -> ExtractRow
            List<ExtractRow> rows;
            await using (var stream = file.OpenReadStream())
            {
                rows = ExcelExtractParser.ParseExtractRows(stream);
            }

            if (rows.Count == 0)
                throw new InvalidOperationException("No valid rows found in the extract.");

            // Attach BatchId + audit
            foreach (var r in rows)
            {
                r.BatchId = batch.Id;
                r.Decision = VerificationDecision.Pending;
                r.DecisionAtUtc = null;
                r.DecisionComment = null;

                r.CreatedBy = uploadedBy;
                r.CreatedAt = DateTime.UtcNow;
            }

            // 5) Insert rows
            _db.ExtractRows.AddRange(rows);
            await _db.SaveChangesAsync(ct);

            // 6) Create assignments from VALUER column
            var assignments = rows
                .Where(r => !string.IsNullOrWhiteSpace(r.Valuer))
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

            // 7) Update statuses
            week.Status = WeekWindowStatus.Uploaded;
            batch.Status = BatchStatus.Uploaded;

            await _db.SaveChangesAsync(ct);

            return batch.Id;
        }

        public async Task NotifyManagersAsync(int batchId, string notifiedBy, CancellationToken ct)
        {
            var batch = await _db.Batches
                .Include(b => b.WeekWindow)
                .FirstOrDefaultAsync(b => b.Id == batchId, ct);

            if (batch is null)
                throw new InvalidOperationException("Batch not found.");

            if (batch.ManagersNotifiedAtUtc != null)
                throw new InvalidOperationException("Managers have already been notified for this batch.");

            // extract file (for filename + audit)
            var extract = await _db.ExtractFiles
                .AsNoTracking()
                .FirstAsync(x => x.BatchId == batchId, ct);

            var baseEmlName = Path.GetFileNameWithoutExtension(extract.OriginalFileName);

            // counts (template expects totals)
            var totalRecords = await _db.ExtractRows.CountAsync(r => r.BatchId == batchId, ct);
            var assignedRecords = await _db.Assignments.CountAsync(a => a.ExtractRow.BatchId == batchId, ct);

            // ✅ ALL managers from DB
            var managerEmails = await _db.SectorManagers
                .AsNoTracking()
                .Where(u => u.IsActive) 
                .Select(u => u.Email)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e!.Trim())
                .Distinct()
                .ToListAsync(ct);

            if (managerEmails.Count == 0)
                throw new InvalidOperationException("No active manager email addresses found.");

            // dashboard url
            var managerDashboardUrl = $"/Manager/Dashboard?batchId={batch.Id}";

            // ✅ use professional EmailTemplates class (from EmailTemplates folder)
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
                To = managerEmails,   // ✅ List<string>
                Subject = subject,
                BodyHtml = html,
                BodyText = text
            };

            // archive eml next to the extract
            var emlPath = Path.Combine(
                Path.GetDirectoryName(_storage.GetExtractFilePath(extract.OriginalFileName))!,
                $"{baseEmlName}.eml");

            // ✅ Safe send + archive
            try
            {
                await _email.SendAndArchiveAsync(req, emlPath, ct);

                _db.EmailArchives.Add(new EmailArchive
                {
                    BatchId = batch.Id,
                    Kind = EmailKind.UploadReadyToManager,
                    ToAddress = string.Join(";", managerEmails),
                    Subject = subject,
                    EmlPath = emlPath,
                    SentAtUtc = DateTime.UtcNow,
                    CreatedBy = notifiedBy,
                    CreatedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                // ⚠️ DO NOT fail the workflow
                _db.EmailArchives.Add(new EmailArchive
                {
                    BatchId = batch.Id,
                    Kind = EmailKind.UploadReadyToManager,
                    ToAddress = string.Join(";", managerEmails),
                    Subject = subject,
                    EmlPath = emlPath,
                    ErrorMessage = ex.Message, // if your table doesn't have this column, remove it
                    CreatedBy = notifiedBy,
                    CreatedAt = DateTime.UtcNow
                });
            }

            batch.ManagersNotifiedAtUtc = DateTime.UtcNow;
            batch.ManagersNotifiedBy = notifiedBy;
            batch.Status = BatchStatus.InVerification;

            await _db.SaveChangesAsync(ct);
        }
        public static class EmailTemplates
{
    public static (string subject, string html, string text) ManagersUploadReady(
        string batchName,
        DateOnly batchDate,
        DateOnly weekStart,
        DateOnly weekEnd,
        DateOnly noticeSendDate,
        int totalRecords,
        int assignedRecords,
        string managerDashboardUrl)
    {
        var subject = $"Valuer Verification Extract Uploaded – {batchName}";

        var html = $@"
<div style='font-family:Segoe UI, Arial, sans-serif; font-size:14px; color:#111;'>
  <p>Good day,</p>

  <p>
    A <strong>Valuer Verification</strong> extract has been uploaded and is ready for verification allocation and tracking.
  </p>

  <table cellpadding='6' cellspacing='0' style='border-collapse:collapse;'>
    <tr><td style='color:#666;'>Batch</td><td><strong>{batchName}</strong> ({batchDate:yyyy-MM-dd})</td></tr>
    <tr><td style='color:#666;'>Week</td><td>{weekStart:yyyy-MM-dd} → {weekEnd:yyyy-MM-dd}</td></tr>
    <tr><td style='color:#666;'>Notice Send Date</td><td><strong>{noticeSendDate:yyyy-MM-dd}</strong></td></tr>
    <tr><td style='color:#666;'>Total Records</td><td>{totalRecords:N0}</td></tr>
    <tr><td style='color:#666;'>Assigned Records</td><td>{assignedRecords:N0}</td></tr>
  </table>

  <p style='margin-top:16px;'>
    Please open the Manager Dashboard to monitor progress and reassign pending items if required:
  </p>

  <p>
    <a href='{managerDashboardUrl}' style='display:inline-block; padding:10px 14px; background:#111; color:#fff; text-decoration:none; border-radius:8px;'>
      Open Manager Dashboard
    </a>
  </p>

  <p style='margin-top:18px; color:#666; font-size:12px;'>
    City of Johannesburg – Valuation Services
  </p>
</div>";

        var text = $@"
Good day,

A Valuer Verification extract has been uploaded and is ready.

Batch: {batchName} ({batchDate:yyyy-MM-dd})
Week: {weekStart:yyyy-MM-dd} → {weekEnd:yyyy-MM-dd}
Notice Send Date: {noticeSendDate:yyyy-MM-dd}
Total Records: {totalRecords:N0}
Assigned Records: {assignedRecords:N0}

Manager Dashboard: {managerDashboardUrl}

City of Johannesburg – Valuation Services
";

        return (subject, html, text);
    }
}

    }
}
