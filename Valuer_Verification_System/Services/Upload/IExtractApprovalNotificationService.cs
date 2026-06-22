using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Infrastructure.Configuration;
using Valuer_Verification_System.Infrastructure.Configuration.Email;
using Valuer_Verification_System.Models.Data;

namespace Valuer_Verification_System.Services.Upload
{
    public interface IExtractApprovalNotificationService
    {
        Task NotifyProjectManagerAfterCleanAsync(int batchId, CancellationToken ct);
    }

    public sealed class ExtractApprovalNotificationService : IExtractApprovalNotificationService
    {
        private readonly VvsDbContext _db;
        private readonly IEmailService _email;
        private readonly EmailSettings _emailCfg;

        public ExtractApprovalNotificationService(
            VvsDbContext db,
            IEmailService email,
            IOptions<EmailSettings> emailCfg)
        {
            _db = db;
            _email = email;
            _emailCfg = emailCfg.Value;
        }

        public async Task NotifyProjectManagerAfterCleanAsync(int batchId, CancellationToken ct)
        {
            var batch = await _db.Batches
                .Include(x => x.WeekWindow)
                .FirstOrDefaultAsync(x => x.Id == batchId, ct);

            if (batch is null)
                throw new InvalidOperationException("Batch not found.");

            if (batch.ExtractCleanedAtUtc == null)
                throw new InvalidOperationException("Extract must be cleaned before Project Manager notification.");

            // Send once only.
            if (batch.ProjectManagerNotifiedAtUtc != null)
                return;

            if (string.IsNullOrWhiteSpace(_emailCfg.ProjectManagerEmail))
                throw new InvalidOperationException("Email:ProjectManagerEmail is missing in appsettings.");

            if (string.IsNullOrWhiteSpace(_emailCfg.NoticeQaManagerEmail))
                throw new InvalidOperationException("Email:NoticeQaManagerEmail is missing in appsettings.");

            if (string.IsNullOrWhiteSpace(_emailCfg.AppBaseUrl))
                throw new InvalidOperationException("Email:AppBaseUrl is missing in appsettings.");

            var totalRows = await _db.ExtractRows
                .AsNoTracking()
                .CountAsync(x => x.BatchId == batchId, ct);

            var totalProperties = await _db.ExtractRows
                .AsNoTracking()
                .Where(x => x.BatchId == batchId)
                .Select(x => x.ValuationKey)
                .Distinct()
                .CountAsync(ct);

            var matchedValuers = await _db.ExtractRows
                .AsNoTracking()
                .Where(x => x.BatchId == batchId &&
                            x.Username != null &&
                            x.Username != "")
                .Select(x => x.Username)
                .Distinct()
                .CountAsync(ct);

            var unmatchedRows = await _db.ExtractRows
                .AsNoTracking()
                .CountAsync(x => x.BatchId == batchId &&
                                 (x.Username == null || x.Username == ""), ct);

            var sectorSummary = await _db.ExtractRows
                .AsNoTracking()
                .Where(x => x.BatchId == batchId)
                .GroupBy(x => x.Sector ?? "Unassigned")
                .Select(g => new SectorSummaryRow
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

            var approveUrl =
                $"{_emailCfg.AppBaseUrl.TrimEnd('/')}/ExtractApproval/Batch/{batch.Id}";

            var sectorRowsHtml = string.Join("", sectorSummary.Select(x => $@"
<tr>
  <td style='padding:8px;border-bottom:1px solid #eee;'>{x.Sector}</td>
  <td style='padding:8px;border-bottom:1px solid #eee;text-align:right;'>{x.TotalProperties:N0}</td>
  <td style='padding:8px;border-bottom:1px solid #eee;text-align:right;'>{x.TotalRows:N0}</td>
  <td style='padding:8px;border-bottom:1px solid #eee;text-align:right;'>{x.TotalValuers:N0}</td>
  <td style='padding:8px;border-bottom:1px solid #eee;text-align:right;'>{x.UnmatchedRows:N0}</td>
</tr>"));

            var subject = $"[VeriApp] Extract cleaned and ready for approval - {batch.BatchName}";

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
              <h2 style='margin:0 0 12px;color:#111;'>Extract cleaned and ready for approval</h2>

              <p style='font-size:14px;color:#333;line-height:1.6;'>
                The folder job found and cleaned a new extract. Please review the summary below.
                If everything is correct, click the button to notify the Sector Managers.
              </p>

              <table width='100%' cellpadding='0' cellspacing='0' style='border-collapse:collapse;margin:18px 0;background:#fafafa;border:1px solid #eee;'>
                {DetailRow("Batch", batch.BatchName)}
                {DetailRow("Extract Week", $"{batch.WeekWindow.WeekStart:yyyy-MM-dd} to {batch.WeekWindow.WeekEnd:yyyy-MM-dd}")}
                {DetailRow("Notice Send Date", $"{batch.WeekWindow.NoticeSendDate:yyyy-MM-dd}")}
                {DetailRow("Clean Week Table", batch.CleanWeekTableName ?? "")}
                {DetailRow("Cleaned At", $"{batch.ExtractCleanedAtUtc:yyyy-MM-dd HH:mm} UTC")}
                {DetailRow("Cleaning Details", batch.ExtractCleaningMessage ?? "")}
                {DetailRow("Cleaned At", $"{batch.ExtractCleanedAtUtc:yyyy-MM-dd HH:mm} UTC")}
                {DetailRow("Total Properties", $"{totalProperties:N0}")}
                {DetailRow("Total Rows", $"{totalRows:N0}")}
                {DetailRow("Matched Valuers", $"{matchedValuers:N0}")}
                {DetailRow("Unmatched Rows", $"{unmatchedRows:N0}")}
              </table>

              <h3 style='margin:20px 0 10px;color:#111;'>Sector Summary</h3>

              <table width='100%' cellpadding='0' cellspacing='0' style='border-collapse:collapse;border:1px solid #eee;'>
                <tr style='background:#111827;color:white;'>
                  <th align='left' style='padding:8px;'>Sector</th>
                  <th align='right' style='padding:8px;'>Properties</th>
                  <th align='right' style='padding:8px;'>Rows</th>
                  <th align='right' style='padding:8px;'>Valuers</th>
                  <th align='right' style='padding:8px;'>Unmatched</th>
                </tr>
                {sectorRowsHtml}
              </table>

              <div style='text-align:center;margin:28px 0;'>
                <a href='{approveUrl}'
                   style='display:inline-block;background:#16a34a;color:#fff;text-decoration:none;
                          padding:13px 28px;border-radius:8px;font-weight:800;'>
                  Review and Notify Sector Managers
                </a>
              </div>

              <p style='font-size:12px;color:#777;border-top:1px solid #eee;padding-top:14px;'>
                Automated email from VeriApp. QA is copied for visibility.
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
Extract cleaned and ready for approval

Batch: {batch.BatchName}
Week: {batch.WeekWindow.WeekStart:yyyy-MM-dd} to {batch.WeekWindow.WeekEnd:yyyy-MM-dd}
Notice Send Date: {batch.WeekWindow.NoticeSendDate:yyyy-MM-dd}
Clean Week Table: {batch.CleanWeekTableName}
Total Properties: {totalProperties:N0}
Total Rows: {totalRows:N0}
Matched Valuers: {matchedValuers:N0}
Unmatched Rows: {unmatchedRows:N0}

Review and notify Sector Managers:
{approveUrl}
";

            var req = new EmailRequest
            {
                To = new List<string> { _emailCfg.ProjectManagerEmail.Trim() },
                Cc = new List<string> { _emailCfg.NoticeQaManagerEmail.Trim() },
                Subject = subject,
                BodyHtml = html,
                BodyText = text
            };

            var extract = await _db.ExtractFiles
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.BatchId == batchId, ct);

            var folder = extract is not null
                ? Path.GetDirectoryName(extract.SavedPath) ?? AppContext.BaseDirectory
                : AppContext.BaseDirectory;

            var emlPath = Path.Combine(
                folder,
                $"{batch.BatchName}_project_manager_approval.eml");

            var sendResult = await _email.SendAndArchiveAsync(req, emlPath, ct);

            _db.EmailArchives.Add(new EmailArchive
            {
                BatchId = batch.Id,
                Kind = EmailKind.UploadReadyToManager,
                ToAddress = $"{_emailCfg.ProjectManagerEmail}; cc:{_emailCfg.NoticeQaManagerEmail}",
                FromAddress = "",
                Subject = subject,
                EmlPath = sendResult.ArchivePath,
                SentAtUtc = DateTime.UtcNow,
                ErrorMessage = sendResult.Sent ? null : sendResult.Error,
                CreatedBy = "folder-job",
                CreatedAt = DateTime.UtcNow
            });

            batch.ProjectManagerNotifiedAtUtc = DateTime.UtcNow;
            batch.ProjectManagerNotifiedTo = _emailCfg.ProjectManagerEmail.Trim();
            batch.QaNotifiedTo = _emailCfg.NoticeQaManagerEmail.Trim();
            batch.UpdatedBy = "folder-job";
            batch.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
        }

        private static string DetailRow(string label, string value)
        {
            return $@"
<tr>
  <td style='padding:8px;border-bottom:1px solid #eee;font-weight:700;width:210px;'>{label}</td>
  <td style='padding:8px;border-bottom:1px solid #eee;'>{value}</td>
</tr>";
        }

        private sealed class SectorSummaryRow
        {
            public string Sector { get; set; } = "";
            public int TotalRows { get; set; }
            public int TotalProperties { get; set; }
            public int TotalValuers { get; set; }
            public int UnmatchedRows { get; set; }
        }
    }
}