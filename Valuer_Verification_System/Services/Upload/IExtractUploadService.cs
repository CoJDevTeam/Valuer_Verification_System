using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Infrastructure.Configuration;
using Valuer_Verification_System.Infrastructure.Configuration.Email;
using Valuer_Verification_System.Infrastructure.Storage;
using Valuer_Verification_System.Models.Data;
using Valuer_Verification_System.Services.Notice;

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
        private readonly IExtractCleaningService _cleaning;

        public ExtractUploadService(VvsDbContext db, IStorageService storage, IEmailService email, IExtractCleaningService cleaning)
        {
            _db = db;
            _storage = storage;
            _email = email;
            _cleaning = cleaning;
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
            await _cleaning.CleanSectorValuerAndResolveUsernameAsync(batch.Id, uploadedBy, ct);

            var cleanedRows = await _db.ExtractRows
                .Where(r => r.BatchId == batch.Id)
                .ToListAsync(ct);

            var assignments = cleanedRows
                .Where(r => !string.IsNullOrWhiteSpace(r.Username))
                .Select(r => new Assignment
                {
                    ExtractRowId = r.Id,
                    AssignedToValuer = r.Username!.Trim(),
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

            // The folder job must clean first.
            if (batch.ExtractCleanedAtUtc == null)
                throw new InvalidOperationException("Extract has not been cleaned yet.");

            if (!string.Equals(batch.ExtractCleaningStatus, "Cleaned", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Extract cleaning was not completed successfully.");

            // Project Manager must approve before Sector Managers are notified.
            if (batch.ProjectManagerApprovedAtUtc == null)
                throw new InvalidOperationException("Project Manager must approve before Sector Managers are notified.");

            // Prevent duplicate sector notifications.
            if (batch.ManagersNotifiedAtUtc != null)
                throw new InvalidOperationException("Sector Managers have already been notified for this batch.");

            var extract = await _db.ExtractFiles
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.BatchId == batchId, ct);

            if (extract is null)
                throw new InvalidOperationException("Extract file log was not found for this batch.");

            var sectorGroups = await _db.ExtractRows
                .AsNoTracking()
                .Where(x => x.BatchId == batchId)
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Sector) ? "Unassigned" : x.Sector!.Trim())
                .Select(g => new SectorNotifySummary
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

            if (sectorGroups.Count == 0)
                throw new InvalidOperationException("No extract rows found for this batch.");

            var managers = await _db.SectorManagers
                .AsNoTracking()
                .Where(x => x.IsActive)
                .ToListAsync(ct);

            if (managers.Count == 0)
                throw new InvalidOperationException("No active Sector Managers found.");

            var sentTo = new List<string>();
            var emailErrors = new List<string>();

            foreach (var sector in sectorGroups)
            {
                var sectorName = sector.Sector.Trim();

                var managerEmails = managers
                    .Where(m =>
                        string.Equals(m.Sector?.Trim(), "*", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(m.Sector?.Trim(), sectorName, StringComparison.OrdinalIgnoreCase))
                    .Select(m => m.Email?.Trim())
                    .Where(e => !string.IsNullOrWhiteSpace(e))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()!;

                if (managerEmails.Count == 0)
                {
                    emailErrors.Add($"No active Sector Manager email found for sector '{sectorName}'.");
                    continue;
                }

                var valuers = await _db.ExtractRows
                    .AsNoTracking()
                    .Where(x =>
                        x.BatchId == batchId &&
                        (string.IsNullOrWhiteSpace(x.Sector) ? "Unassigned" : x.Sector!.Trim()) == sectorName)
                    .GroupBy(x => new
                    {
                        Username = string.IsNullOrWhiteSpace(x.Username) ? "Unmatched" : x.Username!.Trim(),
                        Valuer = string.IsNullOrWhiteSpace(x.Valuer) ? "Unmatched" : x.Valuer!.Trim()
                    })
                    .Select(g => new ValuerNotifySummary
                    {
                        Username = g.Key.Username,
                        Valuer = g.Key.Valuer,
                        TotalRows = g.Count(),
                        TotalProperties = g.Select(x => x.ValuationKey).Distinct().Count()
                    })
                    .OrderByDescending(x => x.TotalProperties)
                    .ThenBy(x => x.Valuer)
                    .ToListAsync(ct);

                var dashboardUrl = $"/Manager/Dashboard?batchId={batch.Id}&sector={Uri.EscapeDataString(sectorName)}";

                var rowsHtml = string.Join("", valuers.Select(v => $@"
<tr>
  <td style='padding:8px;border-bottom:1px solid #eee;'>{Html(v.Valuer)}</td>
  <td style='padding:8px;border-bottom:1px solid #eee;'>{Html(v.Username)}</td>
  <td style='padding:8px;border-bottom:1px solid #eee;text-align:right;'>{v.TotalProperties:N0}</td>
  <td style='padding:8px;border-bottom:1px solid #eee;text-align:right;'>{v.TotalRows:N0}</td>
</tr>"));

                var subject = $"[VeriApp] {sectorName} verification extract ready - {batch.BatchName}";

                var html = $@"
<html>
<body style='margin:0;padding:0;background:#f4f4f4;font-family:Segoe UI,Arial,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='background:#f4f4f4;'>
    <tr>
      <td align='center' style='padding:28px 14px;'>
        <table width='720' cellpadding='0' cellspacing='0' style='background:#fff;border-radius:10px;overflow:hidden;border:1px solid #ddd;'>
          <tr>
            <td style='background:#e6b000;padding:18px 24px;'>
              <strong style='font-size:16px;color:#111;'>Valuer Verification System</strong>
            </td>
          </tr>

          <tr>
            <td style='padding:26px 24px;'>
              <h2 style='margin:0 0 12px;color:#111;'>Verification extract ready: {Html(sectorName)}</h2>

              <p style='font-size:14px;color:#333;line-height:1.6;'>
                The Project Manager has approved the cleaned extract.
                The respective valuers in your sector now have records to verify.
              </p>

              <table width='100%' cellpadding='0' cellspacing='0' style='border-collapse:collapse;margin:18px 0;background:#fafafa;border:1px solid #eee;'>
                {DetailRow("Batch", batch.BatchName)}
                {DetailRow("Sector", sectorName)}
                {DetailRow("Extract Week", $"{batch.WeekWindow.WeekStart:yyyy-MM-dd} to {batch.WeekWindow.WeekEnd:yyyy-MM-dd}")}
                {DetailRow("Notice Send Date", $"{batch.WeekWindow.NoticeSendDate:yyyy-MM-dd}")}
                {DetailRow("Review Period", $"{batch.WeekWindow.ReviewOpenDate:yyyy-MM-dd} to {batch.WeekWindow.ReviewCloseDate:yyyy-MM-dd}")}
                {DetailRow("Clean Week Table", batch.CleanWeekTableName ?? "")}
                {DetailRow("Total Properties", $"{sector.TotalProperties:N0}")}
                {DetailRow("Total Rows", $"{sector.TotalRows:N0}")}
                {DetailRow("Valuers", $"{sector.TotalValuers:N0}")}
                {DetailRow("Unmatched Rows", $"{sector.UnmatchedRows:N0}")}
              </table>

              <h3 style='margin:20px 0 10px;color:#111;'>Valuer Breakdown</h3>

              <table width='100%' cellpadding='0' cellspacing='0' style='border-collapse:collapse;border:1px solid #eee;'>
                <tr style='background:#111827;color:white;'>
                  <th align='left' style='padding:8px;'>Valuer</th>
                  <th align='left' style='padding:8px;'>Username</th>
                  <th align='right' style='padding:8px;'>Properties</th>
                  <th align='right' style='padding:8px;'>Rows</th>
                </tr>
                {rowsHtml}
              </table>

              <div style='text-align:center;margin:28px 0;'>
                <a href='{dashboardUrl}'
                   style='display:inline-block;background:#e6b000;color:#111;text-decoration:none;
                          padding:13px 28px;border-radius:8px;font-weight:800;'>
                  Open Manager Dashboard
                </a>
              </div>

              <p style='font-size:12px;color:#777;border-top:1px solid #eee;padding-top:14px;'>
                Automated notification from VeriApp.
              </p>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";

                var text = $@"
Verification extract ready: {sectorName}

Batch: {batch.BatchName}
Sector: {sectorName}
Week: {batch.WeekWindow.WeekStart:yyyy-MM-dd} to {batch.WeekWindow.WeekEnd:yyyy-MM-dd}
Notice Send Date: {batch.WeekWindow.NoticeSendDate:yyyy-MM-dd}
Review Period: {batch.WeekWindow.ReviewOpenDate:yyyy-MM-dd} to {batch.WeekWindow.ReviewCloseDate:yyyy-MM-dd}
Clean Week Table: {batch.CleanWeekTableName}

Total Properties: {sector.TotalProperties:N0}
Total Rows: {sector.TotalRows:N0}
Valuers: {sector.TotalValuers:N0}
Unmatched Rows: {sector.UnmatchedRows:N0}

Dashboard:
{dashboardUrl}
";

                var req = new EmailRequest
                {
                    To = managerEmails,
                    Subject = subject,
                    BodyHtml = html,
                    BodyText = text
                };

                var baseEmlName = Path.GetFileNameWithoutExtension(extract.OriginalFileName);
                var sourceFolder = Path.GetDirectoryName(extract.SavedPath) ?? AppContext.BaseDirectory;

                var emlPath = Path.Combine(
                    sourceFolder,
                    $"{baseEmlName}_{SanitizeFileName(sectorName)}_sector_managers.eml");

                EmailSendResult sendResult;

                try
                {
                    sendResult = await _email.SendAndArchiveAsync(req, emlPath, ct);
                }
                catch (Exception ex)
                {
                    sendResult = new EmailSendResult
                    {
                        Sent = false,
                        Error = ex.Message,
                        ArchivePath = emlPath
                    };
                }

                _db.EmailArchives.Add(new EmailArchive
                {
                    BatchId = batch.Id,
                    Kind = EmailKind.UploadReadyToManager,
                    ToAddress = string.Join("; ", managerEmails),
                    FromAddress = "",
                    Subject = subject,
                    EmlPath = sendResult.ArchivePath,
                    SentAtUtc = DateTime.UtcNow,
                    ErrorMessage = sendResult.Sent ? null : sendResult.Error,
                    CreatedBy = notifiedBy,
                    CreatedAt = DateTime.UtcNow
                });

                if (sendResult.Sent)
                {
                    sentTo.AddRange(managerEmails);
                }
                else
                {
                    emailErrors.Add($"Sector '{sectorName}' email failed: {sendResult.Error}");
                }
            }

            if (sentTo.Count == 0)
            {
                var error = emailErrors.Count > 0
                    ? string.Join(" | ", emailErrors)
                    : "No Sector Manager emails matched the sectors in this batch.";

                throw new InvalidOperationException(error);
            }

            batch.ManagersNotifiedAtUtc = DateTime.UtcNow;
            batch.ManagersNotifiedBy = notifiedBy;
            batch.Status = BatchStatus.InVerification;
            batch.UpdatedBy = notifiedBy;
            batch.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);

            if (emailErrors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Some sector notifications failed: " + string.Join(" | ", emailErrors));
            }
        }
        private static string DetailRow(string label, string value)
        {
            return $@"
<tr>
  <td style='padding:8px;border-bottom:1px solid #eee;font-weight:700;width:210px;'>{Html(label)}</td>
  <td style='padding:8px;border-bottom:1px solid #eee;'>{Html(value)}</td>
</tr>";
        }

        private static string SanitizeFileName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Unknown";

            var invalid = Path.GetInvalidFileNameChars();

            return string.Concat(value.Select(c => invalid.Contains(c) ? '_' : c));
        }

        private static string Html(string? value)
        {
            return System.Net.WebUtility.HtmlEncode(value ?? "");
        }

        private sealed class SectorNotifySummary
        {
            public string Sector { get; set; } = "";
            public int TotalRows { get; set; }
            public int TotalProperties { get; set; }
            public int TotalValuers { get; set; }
            public int UnmatchedRows { get; set; }
        }

        private sealed class ValuerNotifySummary
        {
            public string Username { get; set; } = "";
            public string Valuer { get; set; } = "";
            public int TotalRows { get; set; }
            public int TotalProperties { get; set; }
        }
    }
}