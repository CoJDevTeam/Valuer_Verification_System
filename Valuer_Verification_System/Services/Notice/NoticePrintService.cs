using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Infrastructure.Configuration;
using Valuer_Verification_System.Models.Data;
using Valuer_Verification_System.Models.Notice;
using Valuer_Verification_System.Models.ViewModels;

namespace Valuer_Verification_System.Services.Notice
{
    public sealed class NoticePrintService : INoticePrintService
    {
        private readonly VvsDbContext _db;
        private readonly INoticePdfService _pdf;
        private readonly StorageSettings _storage;
        private readonly IWebHostEnvironment _env;

        public NoticePrintService(
            VvsDbContext db,
            INoticePdfService pdf,
            IOptions<StorageSettings> storage,
            IWebHostEnvironment env)
        {
            _db = db;
            _pdf = pdf;
            _storage = storage.Value;
            _env = env;
        }

        // ── Folder path ───────────────────────────────────────────────────────
        // C:\Property Verifications\Notices\February 2026\VVS_0001\GV23_LIS_SV_2026_02_EXTRACT_W2

        public string BuildOutputFolder(DateOnly extractionDate, string batchName, string weekTableName)
        {
            var monthFolder = extractionDate.ToString("MMMM yyyy");
            return Path.Combine(
                _storage.RootPath,
                _storage.NoticesFolder,
                monthFolder,
                Sanitize(batchName),
                Sanitize(weekTableName));
        }

        // ── Total count ───────────────────────────────────────────────────────

        public async Task<int> GetTotalNoticeCountAsync(int batchId, CancellationToken ct)
        {
            return await _db.ExtractRows
                .AsNoTracking()
                .Where(r => r.BatchId == batchId
                         && r.SendNotice != null
                         && r.SendNotice.ToUpper() == "Y")
                .Select(r => r.ValuationKey)
                .Distinct()
                .CountAsync(ct);
        }

        // ── Chunk printer ─────────────────────────────────────────────────────

        public async Task<PrintChunkResult> PrintChunkAsync(
            int batchId,
            string weekTableName,
            int offset,
            int chunkSize,
            CancellationToken ct)
        {
            var result = new PrintChunkResult();

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

                var folder = BuildOutputFolder(extractionDate, batch.BatchName, weekTableName);
                Directory.CreateDirectory(folder);

                var ctx = new NoticeContext
                {
                    LetterDate = ww.NoticeSendDate,
                    ReviewOpenDate = ww.ReviewOpenDate,
                    ReviewCloseDate = ww.ReviewCloseDate,
                    LogoPath = Path.Combine(_env.WebRootPath, "Images", "City_of_Joburg_logo.png")
                };

                // All distinct ValuationKeys for this batch (ordered for stable paging)
                // Notice printing is driven by SendNotice == "Y" only.
                // Decision is for the Valuation Verification workflow — separate flow.
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

                // Load rows for chunk
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

                int printed = 0;

                foreach (var key in chunkKeys)
                {
                    if (!groups.TryGetValue(key, out var group)) continue;

                    var mainRow = group[0];

                    // ── Safety net: if address is still missing after cleaning,
                    //    apply the same fallback the SP would have used.
                    //    This ensures every property prints regardless of data quality.
                    if (string.IsNullOrWhiteSpace(mainRow.Addr1))
                    {
                        mainRow.Addr1 = "TO THE REGISTERED OWNER OF:";
                        mainRow.Addr2 = !string.IsNullOrWhiteSpace(mainRow.PropertyDesc)
                            ? mainRow.PropertyDesc
                            : !string.IsNullOrWhiteSpace(mainRow.PropertyDescription)
                                ? mainRow.PropertyDescription
                                : key;
                        mainRow.Addr3 = !string.IsNullOrWhiteSpace(mainRow.TownNameDesc)
                            ? mainRow.TownNameDesc
                            : "GAUTENG";
                        mainRow.Addr4 = "GAUTENG";
                        mainRow.Addr5 = "2000";
                    }

                    bool isMulti = group.Count > 1 ||
                        group.Any(r =>
                            string.Equals(r.CatDesc, "Multipurpose", StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrWhiteSpace(r.ValuationSplitInd));

                    var splitRows = isMulti ? group.Take(4).ToList()
                                            : new List<ExtractRow> { mainRow };
                    try
                    {
                        var pdfBytes = _pdf.GenerateNotice(mainRow, ctx, splitRows);

                        var propDesc = !string.IsNullOrWhiteSpace(mainRow.PropertyDesc)
                            ? mainRow.PropertyDesc
                            : key;
                        var filePath = Path.Combine(folder, Sanitize(propDesc) + ".pdf");

                        await File.WriteAllBytesAsync(filePath, pdfBytes, ct);
                        printed++;
                        result.CurrentProperty = propDesc;
                    }
                    catch (Exception ex) { result.Errors.Add($"{key}: {ex.Message}"); }
                }

                result.Success = true;
                result.Printed = printed;
                // Done when the next chunk would start beyond the total
                // Use offset + chunkSize (not chunkKeys.Count) — matches JS which
                // always advances by CHUNK so both sides stay in sync
                result.Done = (offset + chunkSize) >= result.Total;

                // On the final chunk, mark the batch as printed and awaiting email send
                if (result.Done)
                {
                    var batchToUpdate = await _db.Batches
                        .FirstOrDefaultAsync(b => b.Id == batchId, ct);

                    if (batchToUpdate != null &&
                        batchToUpdate.Status != BatchStatus.Sent) // don't downgrade if already sent
                    {
                        batchToUpdate.Status = BatchStatus.PrintedEmailPending;
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