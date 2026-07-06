using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Infrastructure.Configuration;
using Valuer_Verification_System.Infrastructure.Configuration.Email;
using Valuer_Verification_System.Models.Data;
using Valuer_Verification_System.Models.ViewModels;
using Valuer_Verification_System.Services.Permissions;

namespace Valuer_Verification_System.Services.Notice
{
    public sealed class NoticeQaService : INoticeQaService
    {
        private const int RandomSampleSize = 10;

        private readonly VvsDbContext _db;
        private readonly INoticePrintService _print;
        private readonly INoticeCleaningService _cleaning;
        private readonly IUserService _users;
        private readonly IEmailService _email;
        private readonly EmailSettings _emailCfg;

        public NoticeQaService(
            VvsDbContext db,
            INoticePrintService print,
            INoticeCleaningService cleaning,
            IUserService users,
            IEmailService email,
            IOptions<EmailSettings> emailCfg)
        {
            _db = db;
            _print = print;
            _cleaning = cleaning;
            _users = users;
            _email = email;
            _emailCfg = emailCfg.Value;
        }

        public async Task<bool> IsApprovedAsync(int batchId, CancellationToken ct)
        {
            return await _db.NoticeQaApprovals
                .AsNoTracking()
                .AnyAsync(x => x.BatchId == batchId && x.IsApproved, ct);
        }

        public Task<NoticeQaApproval?> GetApprovalAsync(int batchId, CancellationToken ct)
        {
            return _db.NoticeQaApprovals
                .Include(x => x.Samples)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.BatchId == batchId, ct);
        }

        public async Task<NoticeQaVm> BuildQaAsync(
            int batchId,
            string? weekTableName,
            CancellationToken ct)
        {
            var batch = await _db.Batches
                .Include(b => b.WeekWindow)
                .FirstOrDefaultAsync(b => b.Id == batchId, ct);

            if (batch is null)
                throw new InvalidOperationException("Batch not found.");

            var extractionDate = await _db.ExtractRows
                .AsNoTracking()
                .Where(r => r.BatchId == batchId && r.ExtractionDate.HasValue)
                .Select(r => r.ExtractionDate)
                .FirstOrDefaultAsync(ct) ?? batch.BatchDate;

            weekTableName = !string.IsNullOrWhiteSpace(weekTableName)
                ? weekTableName.Trim()
                : _cleaning.BuildWeekTableName(extractionDate);

            var printFolder = _print.BuildOutputFolder(
                extractionDate,
                batch.BatchName,
                weekTableName);

            var existing = await _db.NoticeQaApprovals
                .Include(x => x.Samples)
                .FirstOrDefaultAsync(x => x.BatchId == batchId, ct);

            if (existing is not null && existing.IsApproved)
                return ToVm(existing, batch.BatchName);

            if (existing is not null)
            {
                _db.NoticeQaSamples.RemoveRange(existing.Samples);
                _db.NoticeQaApprovals.Remove(existing);
                await _db.SaveChangesAsync(ct);
            }

            var candidates = await _db.ExtractRows
                .AsNoTracking()
                .Where(r =>
                    r.BatchId == batchId &&
                    r.Decision == VerificationDecision.Yes)
                .Select(r => new QaCandidate
                {
                    ExtractRowId = r.Id,
                    ValuationKey = r.ValuationKey,
                    Category =
                        r.PropertyCategory ??
                        r.PropertyType ??
                        r.CatDesc ??
                        r.Category ??
                        "Other",
                    PropertyDescription =
                        r.PropertyDesc ??
                        r.StPropertyDescription ??
                        r.PropertyDescription_ ??
                        r.PropertyDescription ??
                        r.ValuationKey
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.ValuationKey))
                .OrderBy(x => Guid.NewGuid())
                .Take(RandomSampleSize)
                .ToListAsync(ct);

            var totalPrinted = await _db.ExtractRows
                .AsNoTracking()
                .CountAsync(r =>
                    r.BatchId == batchId &&
                    r.Decision == VerificationDecision.Yes,
                    ct);

            if (totalPrinted <= 0)
                throw new InvalidOperationException("No printed/verified notices found for QA.");

            var approval = new NoticeQaApproval
            {
                BatchId = batchId,
                WeekTableName = weekTableName,
                PrintFolder = printFolder,
                TotalPrintedNotices = totalPrinted,
                TotalSampled = candidates.Count,
                IsApproved = false,
                CreatedBy = "system",
                CreatedAt = DateTime.UtcNow
            };

            var sampleNo = 1;

            foreach (var item in candidates)
            {
                var pdfPath = ResolvePdfPath(
                    printFolder,
                    item.ValuationKey,
                    item.ExtractRowId,
                    item.PropertyDescription);

                approval.Samples.Add(new NoticeQaSample
                {
                    BatchId = batchId,
                    ValuationKey = item.ValuationKey,
                    Category = string.IsNullOrWhiteSpace(item.Category) ? "Other" : item.Category.Trim(),
                    PropertyDescription = string.IsNullOrWhiteSpace(item.PropertyDescription)
                        ? item.ValuationKey
                        : item.PropertyDescription.Trim(),
                    PdfPath = pdfPath,
                    PdfExists = File.Exists(pdfPath),
                    SampleNo = sampleNo++,

                    // Kept for existing DB/view compatibility.
                    CategoryTotal = totalPrinted,
                    CategorySampleSize = candidates.Count,

                    CreatedBy = "system",
                    CreatedAt = DateTime.UtcNow
                });
            }

            _db.NoticeQaApprovals.Add(approval);
            await _db.SaveChangesAsync(ct);

            return ToVm(approval, batch.BatchName);
        }

        public async Task<string?> GetSamplePdfPathAsync(int sampleId, CancellationToken ct)
        {
            var sample = await _db.NoticeQaSamples
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == sampleId, ct);

            if (sample is null)
                return null;

            return File.Exists(sample.PdfPath) ? sample.PdfPath : null;
        }

        public async Task<NoticeQaApproval> ApproveAsync(
            int batchId,
            string? comments,
            CancellationToken ct)
        {
            var approval = await _db.NoticeQaApprovals
                .Include(x => x.Batch)
                .Include(x => x.Samples)
                .FirstOrDefaultAsync(x => x.BatchId == batchId, ct);

            if (approval is null)
                throw new InvalidOperationException("QA sample has not been created.");

            if (approval.Samples.Count == 0)
                throw new InvalidOperationException("QA sample is empty.");

            var missingPdfCount = approval.Samples.Count(x => !File.Exists(x.PdfPath));

            if (missingPdfCount > 0)
                throw new InvalidOperationException(
                    $"{missingPdfCount} QA sample PDF file(s) are missing. Approval cannot continue.");

            var currentUser = await _users.GetCurrentUserAsync(ct);

            approval.IsApproved = true;
            approval.ApprovedAtUtc = DateTime.UtcNow;
            approval.ApprovedBy = currentUser?.Username ?? "system";
            approval.ApprovedByName = currentUser?.FullName ?? currentUser?.Username ?? "system";
            approval.Comments = comments;
            approval.UpdatedAt = DateTime.UtcNow;
            approval.UpdatedBy = approval.ApprovedBy;

            await _db.SaveChangesAsync(ct);

            await SendDataManagerEmailAsync(approval, ct);

            return approval;
        }

        private async Task SendDataManagerEmailAsync(
            NoticeQaApproval approval,
            CancellationToken ct)
        {
            var to = !string.IsNullOrWhiteSpace(_emailCfg.NoticeQaManagerEmail)
                ? _emailCfg.NoticeQaManagerEmail.Trim()
                : _emailCfg.NoticeContactEmail.Trim();

            var subject = $"[VVS] Notice QA Approved - {approval.Batch.BatchName}";

            var html = $@"
<html>
<body style='font-family:Segoe UI,Arial,sans-serif;background:#f5f5f5;padding:24px;'>
  <div style='max-width:680px;background:#fff;padding:24px;border-radius:8px;border:1px solid #ddd;'>
    <h2 style='margin-top:0;color:#111;'>Notice Quality Assurance Approved</h2>

    <p>Good day,</p>

    <p>
      The mandatory Quality Assurance review has been completed.
      Ten randomly selected printed notices were inspected and approved.
      The batch is now authorised to proceed to the Notice Email stage.
    </p>

    <table style='width:100%;border-collapse:collapse;margin-top:16px;'>
      <tr>
        <td style='padding:8px;border-bottom:1px solid #eee;font-weight:bold;'>Batch</td>
        <td style='padding:8px;border-bottom:1px solid #eee;'>{approval.Batch.BatchName}</td>
      </tr>
      <tr>
        <td style='padding:8px;border-bottom:1px solid #eee;font-weight:bold;'>Week Table</td>
        <td style='padding:8px;border-bottom:1px solid #eee;'>{approval.WeekTableName}</td>
      </tr>
      <tr>
        <td style='padding:8px;border-bottom:1px solid #eee;font-weight:bold;'>Total Printed Notices</td>
        <td style='padding:8px;border-bottom:1px solid #eee;'>{approval.TotalPrintedNotices:N0}</td>
      </tr>
      <tr>
        <td style='padding:8px;border-bottom:1px solid #eee;font-weight:bold;'>Random QA Sample</td>
        <td style='padding:8px;border-bottom:1px solid #eee;'>{approval.TotalSampled:N0}</td>
      </tr>
      <tr>
        <td style='padding:8px;border-bottom:1px solid #eee;font-weight:bold;'>Approved By</td>
        <td style='padding:8px;border-bottom:1px solid #eee;'>{approval.ApprovedByName}</td>
      </tr>
      <tr>
        <td style='padding:8px;border-bottom:1px solid #eee;font-weight:bold;'>Approved At</td>
        <td style='padding:8px;border-bottom:1px solid #eee;'>{approval.ApprovedAtUtc:yyyy-MM-dd HH:mm} UTC</td>
      </tr>
      <tr>
        <td style='padding:8px;border-bottom:1px solid #eee;font-weight:bold;'>Comments</td>
        <td style='padding:8px;border-bottom:1px solid #eee;'>{approval.Comments}</td>
      </tr>
    </table>

    <p style='margin-top:24px;font-size:12px;color:#777;'>
      Automated notification from Valuer Verification System.
    </p>
  </div>
</body>
</html>";

            var text = $@"
Notice Quality Assurance Approved

The mandatory Quality Assurance review has been completed.
Ten randomly selected printed notices were inspected and approved.
The batch is now authorised to proceed to the Notice Email stage.

Batch: {approval.Batch.BatchName}
Week Table: {approval.WeekTableName}
Total Printed Notices: {approval.TotalPrintedNotices:N0}
Random QA Sample: {approval.TotalSampled:N0}
Approved By: {approval.ApprovedByName}
Approved At UTC: {approval.ApprovedAtUtc:yyyy-MM-dd HH:mm}
Comments: {approval.Comments}
";

            var archiveFolder = Path.Combine(approval.PrintFolder, "_QA");
            Directory.CreateDirectory(archiveFolder);

            var archivePath = Path.Combine(
                archiveFolder,
                $"QA_APPROVED_{Sanitize(approval.Batch.BatchName)}.eml");

            var req = new EmailRequest
            {
                To = new List<string> { to },
                Subject = subject,
                BodyHtml = html,
                BodyText = text,
                FromAddress = _emailCfg.NoticeFromAddress,
                FromDisplay = "Valuer Verification System",
                ReplyTo = _emailCfg.NoticeReplyTo
            };

            await _email.SendAndArchiveAsync(req, archivePath, ct);

            approval.DataManagerEmailSentAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        private static NoticeQaVm ToVm(NoticeQaApproval approval, string batchName)
        {
            return new NoticeQaVm
            {
                BatchId = approval.BatchId,
                BatchName = batchName,
                WeekTableName = approval.WeekTableName,
                PrintFolder = approval.PrintFolder,
                TotalPrintedNotices = approval.TotalPrintedNotices,
                TotalSampled = approval.TotalSampled,
                IsApproved = approval.IsApproved,
                ApprovedBy = approval.ApprovedBy,
                ApprovedByName = approval.ApprovedByName,
                ApprovedAtUtc = approval.ApprovedAtUtc,
                Comments = approval.Comments,

                // Kept grouped for your current QA view.
                Categories = approval.Samples
                    .GroupBy(x => x.Category)
                    .OrderBy(g => g.Key)
                    .Select(g => new NoticeQaCategoryVm
                    {
                        Category = g.Key,
                        TotalNotices = approval.TotalPrintedNotices,
                        SampleSize = g.Count(),
                        Samples = g.OrderBy(x => x.SampleNo)
                            .Select(x => new NoticeQaSampleVm
                            {
                                Id = x.Id,
                                SampleNo = x.SampleNo,
                                ValuationKey = x.ValuationKey,
                                Category = x.Category,
                                PropertyDescription = x.PropertyDescription,
                                PdfExists = x.PdfExists
                            })
                            .ToList()
                    })
                    .ToList()
            };
        }

        private static string ResolvePdfPath(
            string printFolder,
            string valuationKey,
            int extractRowId,
            string? propertyDescription)
        {
            var sanitizedValKey = Sanitize(valuationKey);
            var sanitizedDesc = Sanitize(propertyDescription);

            var exactNewName = Path.Combine(
                printFolder,
                $"{sanitizedValKey}_{extractRowId}_{sanitizedDesc}.pdf");

            if (File.Exists(exactNewName))
                return exactNewName;

            var matchingByValKey = Directory.Exists(printFolder)
                ? Directory.GetFiles(printFolder, $"{sanitizedValKey}_*.pdf").FirstOrDefault()
                : null;

            if (!string.IsNullOrWhiteSpace(matchingByValKey))
                return matchingByValKey;

            return Path.Combine(printFolder, $"{sanitizedDesc}.pdf");
        }

        private static string Sanitize(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return "UNKNOWN";

            var bad = Path.GetInvalidFileNameChars()
                .Concat(Path.GetInvalidPathChars())
                .Concat(new[] { ':', '/', '\\' })
                .Distinct()
                .ToArray();

            return string.Concat(s.Trim().Select(c => bad.Contains(c) ? '_' : c))
                .TrimEnd('.');
        }

        private sealed class QaCandidate
        {
            public int ExtractRowId { get; set; }
            public string ValuationKey { get; set; } = "";
            public string Category { get; set; } = "";
            public string PropertyDescription { get; set; } = "";
        }
    }
}