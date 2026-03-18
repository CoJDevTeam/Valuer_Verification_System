using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Models.Data;
using Valuer_Verification_System.Models.ViewModels;

namespace Valuer_Verification_System.Services.Stats
{
    public interface ISection78StatsService
    {
        Task<Section78StatsVm> BuildVmAsync(string financialYear, CancellationToken ct);
        Task<Section78StatEntry> AutoPopulateFromBatchAsync(int batchId, string compiledBy, CancellationToken ct);
        Task SaveEntryAsync(Section78StatEntry entry, string updatedBy, CancellationToken ct);
        Task<byte[]> GenerateExcelAsync(string financialYear, CancellationToken ct);
    }

    public sealed class Section78StatsService : ISection78StatsService
    {
        private readonly VvsDbContext _db;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        public Section78StatsService(VvsDbContext db, IConfiguration config, IWebHostEnvironment env)
        {
            _db = db;
            _config = config;
            _env = env;
        }

        // ── Build VM ──────────────────────────────────────────────────────────
        public async Task<Section78StatsVm> BuildVmAsync(string financialYear, CancellationToken ct)
        {
            // Saved detail rows (editable grid)
            var rows = await _db.Section78Stats
                .AsNoTracking()
                .Where(s => s.FinancialYear == financialYear)
                .OrderBy(s => s.ExtractionDate)
                .ToListAsync(ct);

            // ── Monthly Summary: query SECTION_78_NOTICE week tables directly ──
            // For each distinct YYYY_MM found in existing stats rows, find all
            // matching tables (GV23_LIS_SV_2026_02_%) in SECTION_78_NOTICE,
            // count their rows, and sum = Total Entries for that month.
            var monthly = await BuildMonthlySummaryFromWeekTablesAsync(rows, ct);

            return new Section78StatsVm
            {
                FinancialYear = financialYear,
                Rows = rows,
                MonthlySummary = monthly
            };
        }

        /// <summary>
        /// For each distinct YYYY_MM pattern found in saved stats rows,
        /// query SECTION_78_NOTICE for all matching week tables
        /// (GV23_LIS_SV_2026_02_EXTRACT_W*).
        /// Each table becomes ONE row in the Monthly Summary with its own
        /// row count — tables are NOT summed together.
        /// Stops naturally when no more tables exist for that month pattern.
        /// </summary>
        private async Task<List<Section78StatsVm.MonthlySummaryRow>> BuildMonthlySummaryFromWeekTablesAsync(
            List<Section78StatEntry> savedRows, CancellationToken ct)
        {
            var cs = _config.GetConnectionString("Section78Notice");
            if (string.IsNullOrWhiteSpace(cs))
                return new List<Section78StatsVm.MonthlySummaryRow>();

            // Collect distinct YYYY_MM keys from saved stats
            var monthKeys = new HashSet<string>();

            foreach (var r in savedRows)
            {
                if (r.ExtractionDate.HasValue)
                    monthKeys.Add($"{r.ExtractionDate.Value.Year}_{r.ExtractionDate.Value.Month:D2}");
                else if (!string.IsNullOrWhiteSpace(r.WeekTableName))
                {
                    // GV23_LIS_SV_2026_02_EXTRACT_W2 → parts[3]=2026, parts[4]=02
                    var parts = r.WeekTableName.Split('_');
                    if (parts.Length >= 5)
                        monthKeys.Add($"{parts[3]}_{parts[4]}");
                }
            }

            if (!monthKeys.Any())
                return new List<Section78StatsVm.MonthlySummaryRow>();

            var result = new List<Section78StatsVm.MonthlySummaryRow>();

            await using var conn = new Microsoft.Data.SqlClient.SqlConnection(cs);
            await conn.OpenAsync(ct);

            foreach (var monthKey in monthKeys.OrderBy(k => k))
            {
                // Find all tables matching this month: GV23_LIS_SV_2026_02_%
                var pattern = $"GV23_LIS_SV_{monthKey}_%";

                var findSql = @"
                    SELECT TABLE_NAME
                    FROM   INFORMATION_SCHEMA.TABLES
                    WHERE  TABLE_TYPE = 'BASE TABLE'
                      AND  TABLE_NAME LIKE @pattern
                    ORDER BY TABLE_NAME";

                var tables = new List<string>();
                await using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(findSql, conn))
                {
                    cmd.Parameters.AddWithValue("@pattern", pattern);
                    await using var rdr = await cmd.ExecuteReaderAsync(ct);
                    while (await rdr.ReadAsync(ct))
                        tables.Add(rdr.GetString(0));
                }

                // No tables found for this month — skip, don't add anything
                if (!tables.Any()) continue;

                // Build period display name from key e.g. "2026_02" → "February 2026"
                var keyParts = monthKey.Split('_');
                var periodStr = keyParts.Length == 2
                    && int.TryParse(keyParts[0], out var yr)
                    && int.TryParse(keyParts[1], out var mo)
                    ? new DateTime(yr, mo, 1).ToString("MMMM yyyy")
                    : monthKey;

                // Each table = its own row — NO summing
                foreach (var tableName in tables)
                {
                    // Count rows directly from this specific week table
                    // Table name is from our own system — safe to interpolate
                    var countSql = $"SELECT COUNT(1) FROM [SECTION_78_NOTICE].[dbo].[{tableName}]";
                    int rowCount = 0;
                    await using (var cmd2 = new Microsoft.Data.SqlClient.SqlCommand(countSql, conn))
                    {
                        var scalar = await cmd2.ExecuteScalarAsync(ct);
                        rowCount = Convert.ToInt32(scalar);
                    }

                    // Match to a saved stats entry for this table (for dates/reviews)
                    var saved = savedRows.FirstOrDefault(r =>
                        string.Equals(r.WeekTableName, tableName, StringComparison.OrdinalIgnoreCase));

                    result.Add(new Section78StatsVm.MonthlySummaryRow
                    {
                        Period = periodStr,
                        WeekTableName = tableName,
                        OpenYN = "Y",
                        TotalEntries = rowCount,
                        ReviewPeriodOpen = saved?.ReviewPeriodOpen,
                        ReviewPeriodClosed = saved?.ReviewPeriodClosed,
                        ReviewsReceived = saved?.ReviewsReceived ?? 0,
                        Closed = saved?.Closed
                    });
                }
            }

            return result.OrderBy(r => r.Period).ThenBy(r => r.WeekTableName).ToList();
        }

        // ── Auto-populate counts from a batch ─────────────────────────────────
        public async Task<Section78StatEntry> AutoPopulateFromBatchAsync(
            int batchId, string compiledBy, CancellationToken ct)
        {
            var batch = await _db.Batches
                .Include(b => b.WeekWindow)
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == batchId, ct)
                ?? throw new InvalidOperationException("Batch not found.");

            var ww = batch.WeekWindow;
            var extractDate = await _db.ExtractRows
                .AsNoTracking()
                .Where(r => r.BatchId == batchId && r.ExtractionDate.HasValue)
                .Select(r => r.ExtractionDate)
                .FirstOrDefaultAsync(ct) ?? batch.BatchDate;

            var rows = await _db.ExtractRows
                .AsNoTracking()
                .Where(r => r.BatchId == batchId)
                .ToListAsync(ct);

            int total = rows.Count;
            int splits = rows.Count(r => r.ValuationSplitInd != null &&
                                (r.ValuationSplitInd.Trim() == "Y" || r.ValuationSplitInd.Trim() == "P"));
            int leases = rows.Count(r => r.UnitType != null &&
                                r.UnitType.Trim().Equals("LH", StringComparison.OrdinalIgnoreCase));
            int notPost = rows.Count(r => r.Usable != null && r.Usable.Trim() == "N");
            int suitable = rows.Count(r => r.SendNotice != null &&
                                r.SendNotice.ToUpper() == "Y");
            int emailOnly = rows.Count(r => r.EmailInd != null && r.EmailInd.Trim() == "Y" &&
                                (r.Addr1 == null || r.Addr1.Contains("REGISTERED OWNER")));
            int coj = rows.Count(r => r.CojOwned != null && r.CojOwned.Trim() == "Y");
            int zeroExt = rows.Count(r =>
            {
                var e = r.Extent ?? r.LegalArea;
                if (string.IsNullOrWhiteSpace(e)) return false;
                return decimal.TryParse(e.Trim(),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var d) && d == 0;
            });

            // Build week table name
            var weekNo = (int)Math.Ceiling(extractDate.Day / 7.0);
            var tableName = $"GV23_LIS_SV_{extractDate.Year}_{extractDate.Month:D2}_EXTRACT_W{weekNo}";
            // Period = the month of the batch's NoticeSendDate (WeekWindow)
            // This ensures W1, W2, W3, W4 of the same month all share "February 2026"
            var period = ww.NoticeSendDate.ToString("MMMM yyyy");

            // Week capture period (Mon-Sun of that week)
            var weekStart = extractDate.AddDays(-(int)extractDate.DayOfWeek + 1);
            var weekEnd = weekStart.AddDays(6);
            var capturePeriod = $"{weekStart:yyyy/MM/dd} - {weekEnd:yyyy/MM/dd}";

            // Try to find an existing entry; update or create
            var existing = await _db.Section78Stats
                .FirstOrDefaultAsync(s => s.BatchId == batchId, ct);

            if (existing is null)
            {
                existing = new Section78StatEntry
                {
                    FinancialYear = "2025_26",
                    BatchId = batchId,
                    CreatedBy = compiledBy,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Section78Stats.Add(existing);
            }

            existing.Period = period;
            existing.ExtractionDate = extractDate;
            existing.SvCapturePeriod = capturePeriod;
            existing.WeekTableName = tableName;
            existing.TotalSvEntries = total;
            existing.OrdinaryEntries = total - splits;
            existing.SplitValuesEntries = splits;
            existing.Leases = leases;
            existing.NoticesSuitableForPosting = suitable;
            existing.RemovedEntries = 0;
            existing.EntriesWithZeroExtent = zeroExt;
            existing.NoticesNotSuitable = notPost;
            existing.SuitableForEmailing = suitable;
            existing.SentByEmailNoPostal = emailOnly;
            existing.MunicipalOwnedProperties = coj;
            existing.ReviewPeriodOpen = ww.ReviewOpenDate;
            existing.ReviewPeriodClosed = ww.ReviewCloseDate;
            existing.OpenYN = "Y";
            existing.CompiledBy = compiledBy;
            existing.UpdatedBy = compiledBy;
            existing.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            return existing;
        }

        // ── Save / update an entry ────────────────────────────────────────────
        public async Task SaveEntryAsync(Section78StatEntry entry, string updatedBy, CancellationToken ct)
        {
            var existing = await _db.Section78Stats.FindAsync(new object[] { entry.Id }, ct);
            if (existing is null) throw new InvalidOperationException($"Stat entry {entry.Id} not found.");

            existing.ReviewPeriodOpen = entry.ReviewPeriodOpen;
            existing.ReviewPeriodClosed = entry.ReviewPeriodClosed;
            existing.ReviewsReceived = entry.ReviewsReceived;
            existing.Closed = entry.Closed;
            existing.CompiledBy = entry.CompiledBy;
            existing.UpdatedBy = updatedBy;
            existing.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
        }

        // ── Generate Excel using template ─────────────────────────────────────
        public async Task<byte[]> GenerateExcelAsync(string financialYear, CancellationToken ct)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            var templatePath = Path.Combine(
                _env.WebRootPath, "templates", "Section78_Stats_Template.xlsx");

            var rows = await _db.Section78Stats
                .AsNoTracking()
                .Where(s => s.FinancialYear == financialYear)
                .OrderBy(s => s.ExtractionDate)
                .ToListAsync(ct);

            // Load template and write to a copy in memory
            var templateBytes = await File.ReadAllBytesAsync(templatePath, ct);
            using var ms = new MemoryStream(templateBytes);
            using var pkg = new ExcelPackage(ms);

            // Choose the right sheet
            var sheetName = financialYear == "2024_25"
                ? "Section 78 Summary  (2024_25)"
                : "Section 78 Summary  (2025_26)";

            var ws = pkg.Workbook.Worksheets[sheetName]
                  ?? pkg.Workbook.Worksheets[0];

            // Find the first empty row after header (row 1)
            int startRow = 2;

            // Clear existing data rows (keep header)
            if (ws.Dimension != null && ws.Dimension.Rows > 1)
            {
                ws.DeleteRow(2, ws.Dimension.Rows - 1);
            }

            int row = startRow;
            foreach (var r in rows)
            {
                ws.Cells[row, 1].Value = r.ExtractionDate.HasValue
                    ? r.ExtractionDate.Value.ToDateTime(TimeOnly.MinValue) : (object)DBNull.Value;
                ws.Cells[row, 2].Value = r.SvCapturePeriod;
                ws.Cells[row, 3].Value = r.TotalSvEntries;
                ws.Cells[row, 4].Value = r.OrdinaryEntries;
                ws.Cells[row, 5].Value = r.SplitValuesEntries;
                ws.Cells[row, 6].Value = r.Leases;
                ws.Cells[row, 7].Value = r.NoticesSuitableForPosting;
                ws.Cells[row, 8].Value = r.RemovedEntries;
                ws.Cells[row, 9].Value = r.EntriesWithZeroExtent;
                ws.Cells[row, 10].Value = r.NoticesNotSuitable;
                ws.Cells[row, 11].Value = r.WeekTableName;
                ws.Cells[row, 12].Value = r.SuitableForEmailing;
                ws.Cells[row, 13].Value = r.SentByEmailNoPostal;
                ws.Cells[row, 14].Value = r.MunicipalOwnedProperties;
                ws.Cells[row, 15].Value = r.CompiledBy;
                ws.Cells[row, 16].Value = r.ReviewPeriodOpen.HasValue
                    ? r.ReviewPeriodOpen.Value.ToDateTime(TimeOnly.MinValue) : (object)DBNull.Value;
                ws.Cells[row, 17].Value = r.ReviewPeriodClosed.HasValue
                    ? r.ReviewPeriodClosed.Value.ToDateTime(TimeOnly.MinValue) : (object)DBNull.Value;

                // Date format
                ws.Cells[row, 1].Style.Numberformat.Format = "yyyy/mm/dd";
                ws.Cells[row, 16].Style.Numberformat.Format = "yyyy/mm/dd";
                ws.Cells[row, 17].Style.Numberformat.Format = "yyyy/mm/dd";

                // Alternating row colour
                if (row % 2 == 0)
                {
                    ws.Cells[row, 1, row, 17].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    ws.Cells[row, 1, row, 17].Style.Fill.BackgroundColor.SetColor(
                        System.Drawing.Color.FromArgb(242, 242, 242));
                }
                row++;
            }

            ws.Cells[ws.Dimension.Address].AutoFitColumns();

            using var outMs = new MemoryStream();
            await pkg.SaveAsAsync(outMs, ct);
            return outMs.ToArray();
        }
    }
}