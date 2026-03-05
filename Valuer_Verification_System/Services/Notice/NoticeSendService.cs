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
            int batchId, string weekTableName,
            int offset, int chunkSize,
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

                var ww = batch.WeekWindow;

                var extractionDate = await _db.ExtractRows
                    .AsNoTracking()
                    .Where(r => r.BatchId == batchId && r.ExtractionDate.HasValue)
                    .Select(r => r.ExtractionDate)
                    .FirstOrDefaultAsync(ct) ?? batch.BatchDate;

                var emailFolder = BuildEmailFolder(extractionDate, batch.BatchName, weekTableName);
                Directory.CreateDirectory(emailFolder);

                var ctx = new NoticeContext
                {
                    LetterDate = ww.NoticeSendDate,
                    ReviewOpenDate = ww.ReviewOpenDate,
                    ReviewCloseDate = ww.ReviewCloseDate,
                    LogoPath = Path.Combine(_env.WebRootPath, "Images", "City_of_Joburg_logo.png")
                };

                // All sendable ValuationKeys (stable paging)
                var allKeys = await _db.ExtractRows
                    .AsNoTracking()
                    .Where(r => r.BatchId == batchId
                             && r.SendNotice != null
                             && r.SendNotice.ToUpper() == "Y")
                    .Select(r => r.ValuationKey)
                    .Distinct()
                    .OrderBy(k => k)
                    .ToListAsync(ct);

                result.Total = allKeys.Count;

                var chunkKeys = allKeys.Skip(offset).Take(chunkSize).ToList();
                if (!chunkKeys.Any())
                {
                    result.Success = true;
                    result.Done = true;
                    return result;
                }

                // Load rows for this chunk
                var rows = await _db.ExtractRows
                    .AsNoTracking()
                    .Where(r => r.BatchId == batchId
                             && r.SendNotice != null
                             && r.SendNotice.ToUpper() == "Y"
                             && chunkKeys.Contains(r.ValuationKey))
                    .OrderBy(r => r.ValuationKey).ThenBy(r => r.ValuationSplitNo)
                    .ToListAsync(ct);

                var groups = rows
                    .GroupBy(r => r.ValuationKey)
                    .ToDictionary(g => g.Key, g => g.ToList());

                int sent = 0;

                foreach (var key in chunkKeys)
                {
                    if (!groups.TryGetValue(key, out var group)) continue;

                    var mainRow = group[0];
                    var propDesc = mainRow.PropertyDesc ?? mainRow.ValuationKey ?? key;

                    try
                    {
                        // ── 1. Generate the PDF attachment ────────────────────
                        bool isMulti = group.Count > 1 ||
                            group.Any(r =>
                                string.Equals(r.CatDesc, "Multipurpose", StringComparison.OrdinalIgnoreCase) &&
                                !string.IsNullOrWhiteSpace(r.ValuationSplitInd));

                        var splitRows = isMulti ? group.Take(4).ToList()
                                                : new List<ExtractRow> { mainRow };

                        var pdfBytes = _pdf.GenerateNotice(mainRow, ctx, splitRows);

                        // ── 2. Build email HTML & subject ─────────────────────
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

                        // ── 3. Resolve recipient from cleaned week table ─────
                        // We query SECTION_78_NOTICE week table directly because
                        // the cleaning SPs may have corrected EMAIL_ADDR there.
                        var toAddr = await GetCleanedEmailAsync(
                                         weekTableName, key, ct)
                                     ?? _emailCfg.NoticeContactEmail;

                        // ── 4. Build request ──────────────────────────────────
                        var req = new EmailRequest
                        {
                            To = new List<string> { toAddr },
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
                                    FileName    = Sanitize(propDesc) + ".pdf",
                                    Content     = pdfBytes,
                                    ContentType = "application/pdf"
                                }
                            }
                        };

                        // ── 5. Send + archive .eml ────────────────────────────
                        var emlPath = Path.Combine(emailFolder, Sanitize(propDesc) + ".eml");
                        var sendResult = await _email.SendAndArchiveAsync(req, emlPath, ct);

                        if (!sendResult.Sent && !File.Exists(emlPath))
                            result.Errors.Add($"{propDesc}: {sendResult.Error}");
                        else
                        {
                            sent++;
                            result.CurrentProperty = propDesc;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"{key}: {ex.Message}");
                    }
                }

                result.Success = true;
                result.Sent = sent;
                result.Done = (offset + chunkKeys.Count) >= result.Total;

                // On final chunk — mark batch as Sent
                if (result.Done)
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
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add(ex.Message);
            }

            return result;
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