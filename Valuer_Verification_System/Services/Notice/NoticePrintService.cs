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
                         && r.SendNotice == "Y"
                         && r.Decision == VerificationDecision.Yes)
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
                var allKeys = await _db.ExtractRows
                    .AsNoTracking()
                    .Where(r => r.BatchId == batchId
                             && r.SendNotice == "Y"
                             && r.Decision == VerificationDecision.Yes)
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
                             && r.SendNotice == "Y"
                             && r.Decision == VerificationDecision.Yes
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
                    bool isMulti = group.Count > 1 ||
                        group.Any(r =>
                            string.Equals(r.CatDesc, "Multipurpose", StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrWhiteSpace(r.ValuationSplitInd));

                    var splitRows = isMulti ? group.Take(4).ToList()
                                            : new List<ExtractRow> { mainRow };
                    try
                    {
                        var pdfBytes = _pdf.GenerateNotice(mainRow, ctx, splitRows);

                        var propDesc = mainRow.PropertyDesc ?? mainRow.ValuationKey;
                        var filePath = Path.Combine(folder, Sanitize(propDesc) + ".pdf");

                        await File.WriteAllBytesAsync(filePath, pdfBytes, ct);
                        printed++;
                        result.CurrentProperty = propDesc;
                    }
                    catch (Exception ex) { result.Errors.Add($"{key}: {ex.Message}"); }
                }

                result.Success = true;
                result.Printed = printed;
                result.Done = (offset + chunkKeys.Count) >= result.Total;
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