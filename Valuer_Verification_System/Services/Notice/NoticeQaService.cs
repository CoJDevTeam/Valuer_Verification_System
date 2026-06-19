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

            // If already approved, never rebuild the sample.
            // The approved QA record must remain fixed for audit.
            if (existing is not null && existing.IsApproved)
                return ToVm(existing, batch.BatchName);

            // Rebuild QA sample because printing may have been re-run.
            if (existing is not null)
            {
                _db.NoticeQaSamples.RemoveRange(existing.Samples);
                _db.NoticeQaApprovals.Remove(existing);
                await _db.SaveChangesAsync(ct);
            }

            var rows = await _db.ExtractRows
                .AsNoTracking()
                .Where(r => r.BatchId == batchId
                         && r.SendNotice != null
                         && r.SendNotice.ToUpper() == "Y")
                .OrderBy(r => r.ValuationKey)
                .ThenBy(r => r.ValuationSplitNo)
                .ToListAsync(ct);

            var noticeGroups = rows
                .GroupBy(r => r.ValuationKey)
                .Select(g =>
                {
                    var main = g.First();

                    return new QaCandidate
                    {
                        ValuationKey = main.ValuationKey,
                        Category = ResolveCategory(main),
                        PropertyDescription = ResolvePropertyDescription(main)
                    };
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.ValuationKey))
                .ToList();

            var approval = new NoticeQaApproval
            {
                BatchId = batchId,
                WeekTableName = weekTableName,
                PrintFolder = printFolder,
                TotalPrintedNotices = noticeGroups.Count,
                CreatedBy = "system"
            };

            int sampleNo = 1;

            foreach (var categoryGroup in noticeGroups
                         .GroupBy(x => x.Category)
                         .OrderBy(g => g.Key))
            {
                var categoryTotal = categoryGroup.Count();

                // Mandatory 5%.
                // Minimum 1 sample when category exists.
                var sampleSize = Math.Max(1, (int)Math.Ceiling(categoryTotal * 0.05m));

                var selected = categoryGroup
                    .OrderBy(x => x.ValuationKey)
                    .Take(sampleSize)
                    .ToList();

                foreach (var item in selected)
                {
                    var pdfPath = Path.Combine(
                        printFolder,
                        Sanitize(item.PropertyDescription) + ".pdf");

                    approval.Samples.Add(new NoticeQaSample
                    {
                        BatchId = batchId,
                        ValuationKey = item.ValuationKey,
                        Category = item.Category,
                        PropertyDescription = item.PropertyDescription,
                        PdfPath = pdfPath,
                        PdfExists = File.Exists(pdfPath),
                        SampleNo = sampleNo++,
                        CategoryTotal = categoryTotal,
                        CategorySampleSize = sampleSize
                    });
                }
            }

            approval.TotalSampled = approval.Samples.Count;

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

            if (!File.Exists(sample.PdfPath))
                return null;

            return sample.PdfPath;
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
      The 5% printed notice quality assurance check has been completed and approved.
      The batch can now continue to the sending function.
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
        <td style='padding:8px;border-bottom:1px solid #eee;font-weight:bold;'>QA Sample Checked</td>
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

Batch: {approval.Batch.BatchName}
Week Table: {approval.WeekTableName}
Total Printed Notices: {approval.TotalPrintedNotices:N0}
QA Sample Checked: {approval.TotalSampled:N0}
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
                Categories = approval.Samples
                    .GroupBy(x => x.Category)
                    .OrderBy(g => g.Key)
                    .Select(g => new NoticeQaCategoryVm
                    {
                        Category = g.Key,
                        TotalNotices = g.First().CategoryTotal,
                        SampleSize = g.First().CategorySampleSize,
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

        private static string ResolveCategory(ExtractRow r)
        {
            var category =
                r.PropertyCategory ??
                r.PropertyType ??
                r.CatDesc ??
                r.Category ??
                "Other";

            return category.Trim();
        }

        private static string ResolvePropertyDescription(ExtractRow r)
        {
            var desc =
                r.PropertyDesc ??
                r.StPropertyDescription ??
                r.PropertyDescription_ ??
                r.PropertyDescription ??
                r.ValuationKey;

            return string.IsNullOrWhiteSpace(desc)
                ? r.ValuationKey
                : desc.Trim();
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
            public string ValuationKey { get; set; } = "";
            public string Category { get; set; } = "";
            public string PropertyDescription { get; set; } = "";
        }
    }
}