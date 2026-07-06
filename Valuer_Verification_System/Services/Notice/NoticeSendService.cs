using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Data;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Infrastructure.Configuration;
using Valuer_Verification_System.Infrastructure.Configuration.Email;
using Valuer_Verification_System.Models.Data;
using Valuer_Verification_System.Models.Notice;

namespace Valuer_Verification_System.Services.Notice
{
    public sealed class NoticeSendService : INoticeSendService
    {
        private readonly VvsDbContext _db;
        private readonly INoticePdfService _pdf;
        private readonly IEmailService _email;
        private readonly StorageSettings _storage;
        private readonly EmailSettings _emailCfg;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;

        public NoticeSendService(
            VvsDbContext db,
            INoticePdfService pdf,
            IEmailService email,
            IOptions<StorageSettings> storage,
            IOptions<EmailSettings> emailCfg,
            IWebHostEnvironment env,
            IConfiguration config)
        {
            _db = db;
            _pdf = pdf;
            _email = email;
            _storage = storage.Value;
            _emailCfg = emailCfg.Value;
            _env = env;
            _config = config;
        }

        // ── Folder helpers ────────────────────────────────────────────────────

        public string BuildEmailFolder(DateOnly extractionDate, string batchName, string weekTableName)
        {
            var monthFolder = extractionDate.ToString("MMMM yyyy");
            return Path.Combine(
                _storage.RootPath,
                _storage.NoticesFolder,
                monthFolder,
                Sanitize(batchName),
                Sanitize(weekTableName) + "_EMAIL");   // ← _EMAIL suffix
        }

        // ── Send chunk ────────────────────────────────────────────────────────

        public async Task<SendChunkResult> SendChunkAsync(
      int batchId,
      string weekTableName,
      int offset,
      int chunkSize,
      CancellationToken ct)
        {
            var result = new SendChunkResult();

            try
            {
                var batch = await _db.Batches
                    .Include(b => b.WeekWindow)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Id == batchId, ct);

                if (batch is null)
                {
                    result.Success = false;
                    result.Errors.Add("Batch not found.");
                    return result;
                }

                if (string.IsNullOrWhiteSpace(weekTableName))
                {
                    result.Success = false;
                    result.Errors.Add("Week table name is required.");
                    return result;
                }

                if (chunkSize <= 0)
                    chunkSize = 10;

                if (offset < 0)
                    offset = 0;

                var extractionDate = await _db.ExtractRows
                    .AsNoTracking()
                    .Where(r => r.BatchId == batchId && r.ExtractionDate.HasValue)
                    .Select(r => r.ExtractionDate)
                    .FirstOrDefaultAsync(ct) ?? batch.BatchDate;

                var emailFolder = BuildEmailFolder(extractionDate, batch.BatchName, weekTableName);
                Directory.CreateDirectory(emailFolder);

                var printFolder = BuildPrintFolder(extractionDate, batch.BatchName, weekTableName);

                var ww = batch.WeekWindow;

                var total = await CountWeekTableSendableAsync(batchId, weekTableName, ct);
                result.Total = total;

                var candidates = await GetWeekTableSendCandidatesAsync(
                    batchId,
                    weekTableName,
                    offset,
                    chunkSize,
                    ct);

                if (candidates.Count == 0)
                {
                    result.Success = true;
                    result.Done = true;
                    return result;
                }

                var sent = 0;

                foreach (var item in candidates)
                {
                    try
                    {
                        if (string.Equals(item.SendNoticeStatus, "Y", StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (string.IsNullOrWhiteSpace(item.EmailAddress))
                        {
                            result.Errors.Add($"{item.ValuationKey}: EMAIL_ADDR is empty in week table.");
                            continue;
                        }

                        var row = await ResolveExtractRowAsync(batchId, item, ct);

                        if (row is null)
                        {
                            result.Errors.Add($"{item.ValuationKey}: Could not find matching ExtractRow.");
                            continue;
                        }

                        var propDesc = !string.IsNullOrWhiteSpace(item.PropertyDescription)
                            ? item.PropertyDescription
                            : !string.IsNullOrWhiteSpace(row.PropertyDesc)
                                ? row.PropertyDesc
                                : !string.IsNullOrWhiteSpace(row.PropertyDescription)
                                    ? row.PropertyDescription
                                    : item.ValuationKey;

                        var pdfPath = ResolvePrintedPdfPath(
                            printFolder,
                            row.ValuationKey ?? item.ValuationKey,
                            row.Id,
                            propDesc);

                        if (!File.Exists(pdfPath))
                        {
                            result.Errors.Add($"{item.ValuationKey}: Printed PDF not found. Reprint before sending.");
                            continue;
                        }

                        var pdfBytes = await File.ReadAllBytesAsync(pdfPath, ct);

                        var (subject, html, text) = EmailTemplates.ClientNotice(
                            propertyDescription: propDesc,
                            letterDate: ww.NoticeSendDate,
                            reviewOpenDate: ww.ReviewOpenDate,
                            reviewCloseDate: ww.ReviewCloseDate,
                            contactEmail: _emailCfg.NoticeContactEmail,
                            signOff: _emailCfg.NoticeSignOff,
                            municipalityName: _emailCfg.NoticeMunicipalityName,
                            actReference: _emailCfg.NoticeActReference,
                            subjectPrefix: _emailCfg.NoticeSubjectPrefix);

                        var req = new EmailRequest
                        {
                            To = new List<string> { item.EmailAddress.Trim() },
                            Subject = subject,
                            BodyHtml = html,
                            BodyText = text,
                            FromAddress = _emailCfg.NoticeFromAddress,
                            FromDisplay = _emailCfg.NoticeFromDisplayName,
                            ReplyTo = _emailCfg.NoticeReplyTo,
                            Attachments = new List<EmailAttachment>
                    {
                        new EmailAttachment
                        {
                            FileName = Path.GetFileName(pdfPath),
                            Content = pdfBytes,
                            ContentType = "application/pdf"
                        }
                    }
                        };

                        var emlPath = Path.Combine(
                            emailFolder,
                            $"{Sanitize(row.ValuationKey ?? item.ValuationKey)}_{row.Id}_{Sanitize(propDesc)}.eml");

                        var sendResult = await _email.SendAndArchiveAsync(req, emlPath, ct);

                        if (!sendResult.Sent)
                        {
                            result.Errors.Add($"{propDesc}: {sendResult.Error}");
                            continue;
                        }

                        await MarkWeekTableNoticeSentAsync(
                            weekTableName,
                            item.ExtractRowId,
                            item.ValuationKey,
                            ct);

                        sent++;
                        result.CurrentProperty = propDesc;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"{item.ValuationKey}: {ex.Message}");
                    }
                }

                result.Success = true;
                result.Sent = sent;
                result.Done = (offset + chunkSize) >= result.Total;

                if (result.Done)
                {
                    var remaining = await CountWeekTableUnsentAsync(batchId, weekTableName, ct);

                    if (remaining == 0)
                    {
                        var batchToUpdate = await _db.Batches
                            .Include(b => b.WeekWindow)
                            .FirstOrDefaultAsync(b => b.Id == batchId, ct);

                        if (batchToUpdate != null && !batchToUpdate.NoticesSent)
                        {
                            batchToUpdate.NoticesSent = true;
                            batchToUpdate.NoticesSentAtUtc = DateTime.UtcNow;
                            batchToUpdate.Status = BatchStatus.Sent;
                            batchToUpdate.WeekWindow.Status = WeekWindowStatus.Sent;

                            await _db.SaveChangesAsync(ct);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add(ex.Message);
            }

            return result;
        }

        private string BuildPrintFolder(DateOnly extractionDate, string batchName, string weekTableName)
        {
            var monthFolder = extractionDate.ToString("MMMM yyyy");

            return Path.Combine(
                _storage.RootPath,
                _storage.NoticesFolder,
                monthFolder,
                Sanitize(batchName),
                Sanitize(weekTableName));
        }

        private async Task<int> CountWeekTableSendableAsync(
            int batchId,
            string weekTableName,
            CancellationToken ct)
        {
            var cs = _config.GetConnectionString("Section78Notice")
                ?? throw new InvalidOperationException("Connection string 'Section78Notice' not found.");

            var safeTable = SafeSqlTableName(weekTableName);

            await using var conn = new SqlConnection(cs);
            await conn.OpenAsync(ct);

            var sql = $@"
SELECT COUNT(1)
FROM dbo.{safeTable}
WHERE [BatchId] = @BatchId
  AND NULLIF(LTRIM(RTRIM([EMAIL_ADDR])), '') IS NOT NULL;";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@BatchId", batchId);

            var value = await cmd.ExecuteScalarAsync(ct);
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        private async Task<int> CountWeekTableUnsentAsync(
            int batchId,
            string weekTableName,
            CancellationToken ct)
        {
            var cs = _config.GetConnectionString("Section78Notice")
                ?? throw new InvalidOperationException("Connection string 'Section78Notice' not found.");

            var safeTable = SafeSqlTableName(weekTableName);

            await using var conn = new SqlConnection(cs);
            await conn.OpenAsync(ct);

            var sql = $@"
SELECT COUNT(1)
FROM dbo.{safeTable}
WHERE [BatchId] = @BatchId
  AND NULLIF(LTRIM(RTRIM([EMAIL_ADDR])), '') IS NOT NULL
  AND ISNULL(NULLIF(LTRIM(RTRIM([SendNotices Y/N])), ''), 'N') <> 'Y';";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@BatchId", batchId);

            var value = await cmd.ExecuteScalarAsync(ct);
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        private async Task<List<WeekTableSendCandidate>> GetWeekTableSendCandidatesAsync(
            int batchId,
            string weekTableName,
            int offset,
            int chunkSize,
            CancellationToken ct)
        {
            var cs = _config.GetConnectionString("Section78Notice")
                ?? throw new InvalidOperationException("Connection string 'Section78Notice' not found.");

            var safeTable = SafeSqlTableName(weekTableName);

            var result = new List<WeekTableSendCandidate>();

            await using var conn = new SqlConnection(cs);
            await conn.OpenAsync(ct);

            var sql = $@"
SELECT
    [ExtractRowId],
    [BatchId],
    [VALUATION_KEY],
    [PROPERTY_DESCRIPTION],
    [PROPERTY_DESC],
    [EMAIL_ADDR],
    [SendNotices Y/N]
FROM dbo.{safeTable}
WHERE [BatchId] = @BatchId
  AND NULLIF(LTRIM(RTRIM([EMAIL_ADDR])), '') IS NOT NULL
ORDER BY [ExtractRowId], [VALUATION_KEY]
OFFSET @Offset ROWS FETCH NEXT @ChunkSize ROWS ONLY;";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@BatchId", batchId);
            cmd.Parameters.AddWithValue("@Offset", offset);
            cmd.Parameters.AddWithValue("@ChunkSize", chunkSize);

            await using var reader = await cmd.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                result.Add(new WeekTableSendCandidate
                {
                    ExtractRowId = ReadInt(reader, "ExtractRowId"),
                    BatchId = ReadInt(reader, "BatchId") ?? batchId,
                    ValuationKey = ReadString(reader, "VALUATION_KEY") ?? "",
                    PropertyDescription =
                        ReadString(reader, "PROPERTY_DESCRIPTION")
                        ?? ReadString(reader, "PROPERTY_DESC")
                        ?? "",
                    EmailAddress = ReadString(reader, "EMAIL_ADDR") ?? "",
                    SendNoticeStatus = ReadString(reader, "SendNotices Y/N")
                });
            }

            return result;
        }

        private async Task MarkWeekTableNoticeSentAsync(
            string weekTableName,
            int? extractRowId,
            string valuationKey,
            CancellationToken ct)
        {
            var cs = _config.GetConnectionString("Section78Notice")
                ?? throw new InvalidOperationException("Connection string 'Section78Notice' not found.");

            var safeTable = SafeSqlTableName(weekTableName);

            await using var conn = new SqlConnection(cs);
            await conn.OpenAsync(ct);

            string sql;

            if (extractRowId.HasValue && extractRowId.Value > 0)
            {
                sql = $@"
UPDATE dbo.{safeTable}
SET [SendNotices Y/N] = 'Y'
WHERE [ExtractRowId] = @ExtractRowId;";
            }
            else
            {
                sql = $@"
UPDATE dbo.{safeTable}
SET [SendNotices Y/N] = 'Y'
WHERE [VALUATION_KEY] = @ValuationKey;";
            }

            await using var cmd = new SqlCommand(sql, conn);

            if (extractRowId.HasValue && extractRowId.Value > 0)
                cmd.Parameters.AddWithValue("@ExtractRowId", extractRowId.Value);
            else
                cmd.Parameters.AddWithValue("@ValuationKey", valuationKey);

            await cmd.ExecuteNonQueryAsync(ct);
        }

        private async Task<ExtractRow?> ResolveExtractRowAsync(
            int batchId,
            WeekTableSendCandidate item,
            CancellationToken ct)
        {
            if (item.ExtractRowId.HasValue && item.ExtractRowId.Value > 0)
            {
                return await _db.ExtractRows
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r =>
                        r.BatchId == batchId &&
                        r.Id == item.ExtractRowId.Value,
                        ct);
            }

            return await _db.ExtractRows
                .AsNoTracking()
                .OrderBy(r => r.Id)
                .FirstOrDefaultAsync(r =>
                    r.BatchId == batchId &&
                    r.ValuationKey == item.ValuationKey,
                    ct);
        }

        private static string ResolvePrintedPdfPath(
            string printFolder,
            string valuationKey,
            int extractRowId,
            string propertyDescription)
        {
            var exact = Path.Combine(
                printFolder,
                $"{Sanitize(valuationKey)}_{extractRowId}_{Sanitize(propertyDescription)}.pdf");

            if (File.Exists(exact))
                return exact;

            if (Directory.Exists(printFolder))
            {
                var byRowId = Directory
                    .GetFiles(printFolder, $"*_{extractRowId}_*.pdf")
                    .FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(byRowId))
                    return byRowId;

                var byValKey = Directory
                    .GetFiles(printFolder, $"{Sanitize(valuationKey)}_*.pdf")
                    .FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(byValKey))
                    return byValKey;
            }

            return exact;
        }

        private static string SafeSqlTableName(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new InvalidOperationException("Week table name is required.");

            var clean = tableName.Trim();

            if (!clean.All(c => char.IsLetterOrDigit(c) || c == '_'))
                throw new InvalidOperationException("Invalid week table name.");

            return $"[{clean}]";
        }

        private static string? ReadString(SqlDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal)?.ToString();
        }

        private static int? ReadInt(SqlDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            if (reader.IsDBNull(ordinal)) return null;
            return Convert.ToInt32(reader.GetValue(ordinal));
        }

        private sealed class WeekTableSendCandidate
        {
            public int? ExtractRowId { get; set; }
            public int BatchId { get; set; }
            public string ValuationKey { get; set; } = "";
            public string PropertyDescription { get; set; } = "";
            public string EmailAddress { get; set; } = "";
            public string? SendNoticeStatus { get; set; }
        }

        /// <summary>
        /// Queries EMAIL_ADDR for the given ValuationKey directly from the
        /// cleaned week table in [SECTION_78_NOTICE], which reflects any
        /// corrections made during the cleaning pipeline.
        /// Returns null if empty/whitespace or row not found.
        /// </summary>
        private async Task<string?> GetCleanedEmailAsync(
      string weekTableName, string valuationKey, CancellationToken ct)
        {
            var cs = _config.GetConnectionString("Section78Notice");
            if (string.IsNullOrWhiteSpace(cs)) return null;

            try
            {
                await using var conn = new SqlConnection(cs);
                await conn.OpenAsync(ct);

                await using var cmd = new SqlCommand("sp_VVS_GetCleanedEmail", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@WeekTableName", weekTableName);
                cmd.Parameters.AddWithValue("@ValuationKey", valuationKey);

                var result = await cmd.ExecuteScalarAsync(ct);

                var email = result as string;

                return string.IsNullOrWhiteSpace(email) ? null : email.Trim();
            }
            catch
            {
                // Week table may not exist yet
                return null;
            }
        }
        private static string Sanitize(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "UNKNOWN";
            var bad = Path.GetInvalidFileNameChars()
                         .Concat(Path.GetInvalidPathChars())
                         .Concat(new[] { ':', '/', '\\' })
                         .Distinct().ToArray();
            return string.Concat(s.Trim().Select(c => bad.Contains(c) ? '_' : c)).TrimEnd('.');
        }
    }
}