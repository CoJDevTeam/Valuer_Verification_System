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

        public async Task<int> UploadAsync(int weekWindowId, IFormFile file, string uploadedBy, CancellationToken ct)
        {
            if (file == null || file.Length <= 0)
                throw new ArgumentException("Please select a valid Excel file.");

            var week = await _db.WeekWindows.FirstOrDefaultAsync(x => x.Id == weekWindowId, ct);
            if (week is null)
                throw new InvalidOperationException("Week window not found.");

            _storage.EnsureRootStructure();

            // 1) Save file (Extracts folder) - keep original name
            var savedExtractPath = await _storage.SaveExtractAsync(file, ct);

            // 2) Create Batch (name derived from Batch Id for concurrency safety)
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

            // Attach BatchId and insert
            foreach (var r in rows)
            {
                r.BatchId = batch.Id;
                r.CreatedBy = uploadedBy;
                r.CreatedAt = DateTime.UtcNow;
            }

            // 5) Insert rows
            _db.ExtractRows.AddRange(rows);
            await _db.SaveChangesAsync(ct);

            // 6) Create assignments from VALUER (valuer inbox)
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

            // Update statuses (uploaded but NOT notified yet)
            week.Status = WeekWindowStatus.Uploaded;
            batch.Status = BatchStatus.Uploaded;

            await _db.SaveChangesAsync(ct);

            return batch.Id;
        }

        public async Task NotifyManagersAsync(int batchId, string user, CancellationToken ct)
        {
            var batch = await _db.Batches
                .Include(b => b.WeekWindow)
                .FirstOrDefaultAsync(b => b.Id == batchId, ct);

            if (batch is null) throw new InvalidOperationException("Batch not found.");

            if (batch.ManagersNotifiedAtUtc != null)
                throw new InvalidOperationException("Managers have already been notified for this batch.");

            var rows = await _db.ExtractRows
                .Where(r => r.BatchId == batchId)
                .ToListAsync(ct);

            var sectors = rows
                .Select(x => x.Sector?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (sectors.Count == 0) sectors.Add("Unknown");

            // Base name for .eml
            var extract = await _db.ExtractFiles.FirstAsync(x => x.BatchId == batchId, ct);
            var baseEmlName = Path.GetFileNameWithoutExtension(extract.OriginalFileName);

            foreach (var sector in sectors)
            {
                var req = new EmailRequest
                {
                    To = "JabulaniSib@joburg.org.za",
                    Subject = $"VVS Extract Ready – {sector} – {batch.BatchName}",
                    BodyText =
        $"""
Good day,

The valuation verification extract is uploaded and ready for verification.

Sector: {sector}
Batch: {batch.BatchName}
Week: {batch.WeekWindow.WeekStart:yyyy-MM-dd} (Mon) to {batch.WeekWindow.WeekEnd:yyyy-MM-dd} (Sun)
Notice Send Date: {batch.WeekWindow.NoticeSendDate:yyyy-MM-dd}
File: {extract.OriginalFileName}

Regards,
City of Johannesburg
Valuation Services
"""
                };

                var safeSector = sector.Replace(" ", "_").Replace("/", "_");
                var emlName = sectors.Count == 1 ? $"{baseEmlName}.eml" : $"{baseEmlName}_{safeSector}.eml";

                // Save in Extracts folder
                var emlPath = Path.Combine(
                    Path.GetDirectoryName(_storage.GetExtractFilePath(extract.OriginalFileName))!,
                    emlName);

                await _email.SendAndArchiveAsync(req, emlPath, ct);

                _db.EmailArchives.Add(new EmailArchive
                {
                    BatchId = batch.Id,
                    Kind = EmailKind.UploadReadyToManager,
                    ToAddress = req.To,
                    FromAddress = "", // optional
                    Subject = req.Subject,
                    EmlPath = emlPath,
                    Sector = sector,
                    SentAtUtc = DateTime.UtcNow,
                    CreatedBy = user,
                    CreatedAt = DateTime.UtcNow
                });
            }

            batch.ManagersNotifiedAtUtc = DateTime.UtcNow;
            batch.ManagersNotifiedBy = user;
            batch.Status = BatchStatus.InVerification;

            await _db.SaveChangesAsync(ct);
        }

    }
}
