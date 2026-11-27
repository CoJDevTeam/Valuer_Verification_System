using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;

namespace Valuer_Verification_System.Services.Notice
{
    /// <summary>
    /// "Released" properties = verified Yes by the valuers (saved in the week table) and signed off by QA.
    /// The week table is the only place the final decision lives, so the notice steps read it from here.
    /// </summary>
    public static class ReleasedRows
    {
        private static async Task<string?> WeekAsync(VvsDbContext db, int batchId, CancellationToken ct)
        {
            var n = await db.Batches.AsNoTracking().Where(b => b.Id == batchId).Select(b => b.CleanWeekTableName).FirstOrDefaultAsync(ct);
            return string.IsNullOrWhiteSpace(n) ? null : WeekTableSql.Full(n);
        }

        private static Task<int> CountAsync(VvsDbContext db, string sql, CancellationToken ct) =>
            db.Database.SqlQueryRaw<int>(sql).FirstAsync(ct);

        /// <summary>Pending / Yes / No rows in the week table, and whether the Senior Manager signed the batch off.</summary>
        public static async Task<(int pending, int yes, int no, bool signedOff)> ReleaseSummaryAsync(this VvsDbContext db, int batchId, CancellationToken ct)
        {
            var t = await WeekAsync(db, batchId, ct);
            if (t == null) return (0, 0, 0, false);
            var pending = await CountAsync(db, $"SELECT COUNT(*) AS [Value] FROM {t} WHERE ISNULL(Decision,'Pending') NOT IN ('Yes','No')", ct);
            var yes = await CountAsync(db, $"SELECT COUNT(*) AS [Value] FROM {t} WHERE Decision = 'Yes'", ct);
            var no = await CountAsync(db, $"SELECT COUNT(*) AS [Value] FROM {t} WHERE Decision = 'No'", ct);
            var signed = false;
            try { signed = await CountAsync(db, $"SELECT COUNT(*) AS [Value] FROM dbo.QaSignoff WHERE BatchId = {batchId}", ct) > 0; }
            catch { signed = false; }
            return (pending, yes, no, signed);
        }

        /// <summary>Number of properties (distinct VALUATION_KEY) verified Yes, and how many of them have several rows (multipurpose).</summary>
        public static async Task<(int total, int multi)> YesCountsAsync(this VvsDbContext db, int batchId, CancellationToken ct)
        {
            var t = await WeekAsync(db, batchId, ct);
            if (t == null) return (0, 0);
            try
            {
                var total = await CountAsync(db, $"SELECT COUNT(DISTINCT VALUATION_KEY) AS [Value] FROM {t} WHERE Decision = 'Yes'", ct);
                var multi = await CountAsync(db, $"SELECT COUNT(*) AS [Value] FROM (SELECT VALUATION_KEY FROM {t} WHERE Decision = 'Yes' GROUP BY VALUATION_KEY HAVING COUNT(*) > 1) x", ct);
                return (total, multi);
            }
            catch { return (0, 0); }
        }

        /// <summary>ExtractRows.Id of the first Yes property, for the PDF preview.</summary>
        public static async Task<int?> FirstYesExtractRowIdAsync(this VvsDbContext db, int batchId, CancellationToken ct)
        {
            var t = await WeekAsync(db, batchId, ct);
            if (t == null) return null;
            try
            {
                var ids = await db.Database
                    .SqlQueryRaw<int>($"SELECT TOP 1 CAST(ExtractRowId AS int) AS [Value] FROM {t} WHERE Decision = 'Yes' AND ExtractRowId IS NOT NULL")
                    .ToListAsync(ct);
                return ids.Count > 0 ? ids[0] : null;
            }
            catch { return null; }
        }
    }
}
