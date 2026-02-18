using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Infrastructure.Storage;
using Valuer_Verification_System.Models.Data;

namespace Valuer_Verification_System.Services.Stats
{

    public interface IStatsExportService
    {
        Task<(string FileName, string SavedPath, byte[] Bytes)> GenerateBatchStatsAsync(int batchId, string generatedBy, CancellationToken ct);
    }

    public sealed class StatsExportService : IStatsExportService
    {
        private readonly VvsDbContext _db;
        private readonly IStorageService _storage;

        public StatsExportService(VvsDbContext db, IStorageService storage)
        {
            _db = db;
            _storage = storage;
        }

        public async Task<(string FileName, string SavedPath, byte[] Bytes)> GenerateBatchStatsAsync(int batchId, string generatedBy, CancellationToken ct)
        {
            var batch = await _db.Batches
                .Include(b => b.WeekWindow)
                .FirstOrDefaultAsync(b => b.Id == batchId, ct);

            if (batch is null)
                throw new InvalidOperationException("Batch not found.");

            var rows = await _db.ExtractRows
                .AsNoTracking()
                .Where(r => r.BatchId == batchId)
                .ToListAsync(ct);

            var total = rows.Count;
            var pending = rows.Count(r => r.Decision == VerificationDecision.Pending);
            var yes = rows.Count(r => r.Decision == VerificationDecision.Yes);
            var no = rows.Count(r => r.Decision == VerificationDecision.No);
            var verified = yes + no;
            var progress = total == 0 ? 0m : Math.Round((decimal)verified * 100m / total, 2);

            // By Sector
            var bySector = rows
                .GroupBy(r => (r.Sector ?? "Unknown").Trim())
                .Select(g => new
                {
                    Sector = g.Key,
                    Total = g.Count(),
                    Pending = g.Count(x => x.Decision == VerificationDecision.Pending),
                    Yes = g.Count(x => x.Decision == VerificationDecision.Yes),
                    No = g.Count(x => x.Decision == VerificationDecision.No),
                })
                .OrderByDescending(x => x.Pending)
                .ThenBy(x => x.Sector)
                .ToList();

            // By Valuer (use Assignments so it matches inbox reality)
            var byValuer = await _db.Assignments
                .AsNoTracking()
                .Where(a => a.ExtractRow.BatchId == batchId)
                .GroupBy(a => a.AssignedToValuer)
                .Select(g => new
                {
                    Valuer = g.Key,
                    Total = g.Count(),
                    Pending = g.Count(x => x.ExtractRow.Decision == VerificationDecision.Pending),
                    Yes = g.Count(x => x.ExtractRow.Decision == VerificationDecision.Yes),
                    No = g.Count(x => x.ExtractRow.Decision == VerificationDecision.No),
                })
                .OrderByDescending(x => x.Pending)
                .ThenBy(x => x.Valuer)
                .ToListAsync(ct);

            // "No" details list (helpful to managers)
            var noList = rows
                .Where(r => r.Decision == VerificationDecision.No)
                .OrderBy(r => r.Sector)
                .ThenBy(r => r.ValuationKey)
                .Select(r => new
                {
                    r.ValuationKey,
                    r.Sector,
                    r.Valuer,
                    r.PropertyDescription,
                    r.MarketValue,
                    r.DecisionComment,
                    r.DecisionAtUtc
                })
                .ToList();

            using var wb = new XLWorkbook();

            // Sheet 1: Summary
            var ws1 = wb.Worksheets.Add("Summary");
            ws1.Cell(1, 1).Value = "Batch";
            ws1.Cell(1, 2).Value = batch.BatchName;

            ws1.Cell(2, 1).Value = "Batch Date";
            ws1.Cell(2, 2).Value = batch.BatchDate.ToString("yyyy-MM-dd");

            ws1.Cell(3, 1).Value = "Week Start (Mon)";
            ws1.Cell(3, 2).Value = batch.WeekWindow.WeekStart.ToString("yyyy-MM-dd");

            ws1.Cell(4, 1).Value = "Week End (Sun)";
            ws1.Cell(4, 2).Value = batch.WeekWindow.WeekEnd.ToString("yyyy-MM-dd");

            ws1.Cell(5, 1).Value = "Notice Send Date";
            ws1.Cell(5, 2).Value = batch.WeekWindow.NoticeSendDate.ToString("yyyy-MM-dd");

            ws1.Cell(7, 1).Value = "Total";
            ws1.Cell(7, 2).Value = total;

            ws1.Cell(8, 1).Value = "Pending";
            ws1.Cell(8, 2).Value = pending;

            ws1.Cell(9, 1).Value = "Yes";
            ws1.Cell(9, 2).Value = yes;

            ws1.Cell(10, 1).Value = "No";
            ws1.Cell(10, 2).Value = no;

            ws1.Cell(11, 1).Value = "Progress %";
            ws1.Cell(11, 2).Value = progress;

            ws1.Cell(13, 1).Value = "Generated By";
            ws1.Cell(13, 2).Value = generatedBy;

            ws1.Cell(14, 1).Value = "Generated At (UTC)";
            ws1.Cell(14, 2).Value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");

            ws1.Columns().AdjustToContents();

            // Sheet 2: By Valuer
            var ws2 = wb.Worksheets.Add("By Valuer");
            ws2.Cell(1, 1).Value = "Valuer";
            ws2.Cell(1, 2).Value = "Total";
            ws2.Cell(1, 3).Value = "Pending";
            ws2.Cell(1, 4).Value = "Yes";
            ws2.Cell(1, 5).Value = "No";
            ws2.Cell(1, 6).Value = "Progress %";

            var r2 = 2;
            foreach (var v in byValuer)
            {
                var vVerified = v.Yes + v.No;
                var vProgress = v.Total == 0 ? 0m : Math.Round((decimal)vVerified * 100m / v.Total, 2);

                ws2.Cell(r2, 1).Value = v.Valuer;
                ws2.Cell(r2, 2).Value = v.Total;
                ws2.Cell(r2, 3).Value = v.Pending;
                ws2.Cell(r2, 4).Value = v.Yes;
                ws2.Cell(r2, 5).Value = v.No;
                ws2.Cell(r2, 6).Value = vProgress;
                r2++;
            }
            ws2.Columns().AdjustToContents();

            // Sheet 3: By Sector
            var ws3 = wb.Worksheets.Add("By Sector");
            ws3.Cell(1, 1).Value = "Sector";
            ws3.Cell(1, 2).Value = "Total";
            ws3.Cell(1, 3).Value = "Pending";
            ws3.Cell(1, 4).Value = "Yes";
            ws3.Cell(1, 5).Value = "No";
            ws3.Cell(1, 6).Value = "Progress %";

            var r3 = 2;
            foreach (var s in bySector)
            {
                var sVerified = s.Yes + s.No;
                var sProgress = s.Total == 0 ? 0m : Math.Round((decimal)sVerified * 100m / s.Total, 2);

                ws3.Cell(r3, 1).Value = s.Sector;
                ws3.Cell(r3, 2).Value = s.Total;
                ws3.Cell(r3, 3).Value = s.Pending;
                ws3.Cell(r3, 4).Value = s.Yes;
                ws3.Cell(r3, 5).Value = s.No;
                ws3.Cell(r3, 6).Value = sProgress;
                r3++;
            }
            ws3.Columns().AdjustToContents();

            // Sheet 4: No Details
            var ws4 = wb.Worksheets.Add("No Details");
            ws4.Cell(1, 1).Value = "ValuationKey";
            ws4.Cell(1, 2).Value = "Sector";
            ws4.Cell(1, 3).Value = "Valuer";
            ws4.Cell(1, 4).Value = "PropertyDescription";
            ws4.Cell(1, 5).Value = "MarketValue";
            ws4.Cell(1, 6).Value = "Comment";
            ws4.Cell(1, 7).Value = "DecisionAtUtc";

            var r4 = 2;
            foreach (var n in noList)
            {
                ws4.Cell(r4, 1).Value = n.ValuationKey;
                ws4.Cell(r4, 2).Value = n.Sector;
                ws4.Cell(r4, 3).Value = n.Valuer;
                ws4.Cell(r4, 4).Value = n.PropertyDescription;
                ws4.Cell(r4, 5).Value = n.MarketValue;
                ws4.Cell(r4, 6).Value = n.DecisionComment;
                ws4.Cell(r4, 7).Value = n.DecisionAtUtc?.ToString("yyyy-MM-dd HH:mm");
                r4++;
            }
            ws4.Columns().AdjustToContents();

            // Export bytes
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            var bytes = ms.ToArray();

            // File name + save to Stats folder
            var dt = DateTime.Today.ToString("yyyyMMdd");
            var fileName = $"{batch.BatchName}_Stats_{dt}.xlsx";

            var savedPath = await _storage.SaveStatsAsync(fileName, bytes, ct);

            return (fileName, savedPath, bytes);
        }
    }
}
