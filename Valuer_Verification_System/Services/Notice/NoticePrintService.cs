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
            // Verified rows (Decision = Yes) live in the week table, not in ExtractRows.
            var wk = await _db.Batches.AsNoTracking().Where(b => b.Id == batchId)
                .Select(b => b.CleanWeekTableName).FirstOrDefaultAsync(ct);
            if (string.IsNullOrWhiteSpace(wk)) return 0;
            return await _db.Database
                .SqlQueryRaw<int>($"SELECT COUNT(DISTINCT VALUATION_KEY) AS [Value] FROM {WeekTableSql.Full(wk)} WHERE Decision = 'Yes'")
                .FirstAsync(ct);
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

                if (string.IsNullOrWhiteSpace(weekTableName))
                {
                    result.Success = false;
                    result.Errors.Add("Week table name is required.");
                    return result;
                }

                if (chunkSize <= 0)
                    chunkSize = 50;

                if (offset < 0)
                    offset = 0;

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

                // Print every property the valuers verified Yes. The decision is saved in the week table
                // (ExtractRowId = ExtractRows.Id), so read the ids from there.
                var yesIds = await _db.Database
                    .SqlQueryRaw<int>($"SELECT CAST(ExtractRowId AS int) AS [Value] FROM {WeekTableSql.Full(weekTableName)} WHERE Decision = 'Yes' AND ExtractRowId IS NOT NULL")
                    .ToListAsync(ct);

                var allRowIds = await _db.ExtractRows
                    .AsNoTracking()
                    .Where(r => r.BatchId == batchId && yesIds.Contains(r.Id))
                    .OrderBy(r => r.UnitKey)
                    .ThenBy(r => r.PremiseId)
                    .ThenBy(r => r.ValuationKey)
                    .ThenBy(r => r.Id)
                    .Select(r => r.Id)
                    .ToListAsync(ct);

                result.Total = allRowIds.Count;

                var chunkRowIds = allRowIds
                    .Skip(offset)
                    .Take(chunkSize)
                    .ToList();

                if (!chunkRowIds.Any())
                {
                    result.Success = true;
                    result.Done = true;
                    return result;
                }

                var rows = await _db.ExtractRows
                    .AsNoTracking()
                    .Where(r => chunkRowIds.Contains(r.Id))
                    .OrderBy(r => r.UnitKey)
                    .ThenBy(r => r.PremiseId)
                    .ThenBy(r => r.ValuationKey)
                    .ThenBy(r => r.Id)
                    .ToListAsync(ct);

                var printed = 0;

                foreach (var mainRow in rows)
                {
                    var key = mainRow.ValuationKey ?? mainRow.Id.ToString();

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

                    var splitRows = new List<ExtractRow> { mainRow };

                    try
                    {
                        var pdfBytes = _pdf.GenerateNotice(mainRow, ctx, splitRows);

                        var propDesc = !string.IsNullOrWhiteSpace(mainRow.PropertyDesc)
                            ? mainRow.PropertyDesc
                            : !string.IsNullOrWhiteSpace(mainRow.PropertyDescription)
                                ? mainRow.PropertyDescription
                                : key;

                        // ✅ Unique file name prevents overwrites
                        var fileName =
                            $"{Sanitize(mainRow.ValuationKey ?? "NO_VALKEY")}_{mainRow.Id}_{Sanitize(propDesc)}.pdf";

                        var filePath = Path.Combine(folder, fileName);

                        await File.WriteAllBytesAsync(filePath, pdfBytes, ct);

                        printed++;
                        result.CurrentProperty = propDesc;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"{key} / RowId {mainRow.Id}: {ex.Message}");
                    }
                }

                result.Success = true;
                result.Printed = printed;
                result.Done = (offset + chunkSize) >= result.Total;

                if (result.Done)
                {
                    var batchToUpdate = await _db.Batches
                        .FirstOrDefaultAsync(b => b.Id == batchId, ct);

                    if (batchToUpdate != null &&
                        batchToUpdate.Status != BatchStatus.Sent)
                    {
                        batchToUpdate.Status = BatchStatus.PrintedEmailPending;
                        batchToUpdate.UpdatedAt = DateTime.UtcNow;

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