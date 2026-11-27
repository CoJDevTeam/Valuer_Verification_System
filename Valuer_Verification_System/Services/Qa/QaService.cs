using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;

namespace Valuer_Verification_System.Services.Qa
{
    public sealed class QaSectorRow { public string Sector = ""; public int Total, Yes, No, Pending, Returned; public bool Approved; public string? ApprovedBy; }
    public sealed class QaItem
    {
        public string Key = "", Sector = "", Premise = "", Description = "", Town = "", Valuer = "", Decision = "", Comment = "", Owner = "";
        public decimal? MarketValue; public string? DecisionBy; public DateTime? DecisionAtUtc; public bool Returned;
    }
    public sealed class QaBatch { public int Id; public string Name = ""; }

    public interface IQaService
    {
        Task<List<QaBatch>> BatchesAsync(CancellationToken ct);
        Task<List<QaSectorRow>> SectorsAsync(int batchId, CancellationToken ct);
        Task<List<QaItem>> ListAsync(int batchId, string sector, string filter, CancellationToken ct);
        Task<(bool ok, string msg)> SendBackAsync(int batchId, string key, string note, string by, CancellationToken ct);
        Task<(bool ok, string msg)> ApproveSectorAsync(int batchId, string sector, string by, CancellationToken ct);
        Task<(bool ok, string msg)> SignOffAsync(int batchId, string by, string? note, CancellationToken ct);
        Task<(bool signed, string? by, DateTime? at)> SignoffAsync(int batchId, CancellationToken ct);
    }

    public sealed class QaService : IQaService
    {
        private readonly VvsDbContext _db;
        public QaService(VvsDbContext db) { _db = db; }

        // ---- helpers -------------------------------------------------------
        private const string Eff = "ISNULL(NULLIF(LTRIM(RTRIM(w.DevAllocatedTo)),''), NULLIF(LTRIM(RTRIM(w.Username)),''))";

        private async Task<string> TableAsync(int batchId, CancellationToken ct)
        {
            var n = await _db.Batches.AsNoTracking().Where(b => b.Id == batchId).Select(b => b.CleanWeekTableName).FirstOrDefaultAsync(ct);
            if (string.IsNullOrWhiteSpace(n)) throw new InvalidOperationException("This batch has no week table yet.");
            n = n.Trim(); var dot = n.LastIndexOf('.'); if (dot >= 0) n = n[(dot + 1)..];
            n = n.Trim('[', ']');
            if (!Regex.IsMatch(n, "^[A-Za-z0-9_]+$")) throw new InvalidOperationException("Invalid week table name.");
            return $"[SECTION_78_NOTICE].[dbo].[{n}]";
        }

        private async Task<SqlConnection> OpenAsync(CancellationToken ct)
        {
            var c = new SqlConnection(_db.Database.GetConnectionString());
            await c.OpenAsync(ct); return c;
        }
        private static SqlCommand Cmd(SqlConnection c, string sql, params (string, object?)[] p)
        {
            var cmd = new SqlCommand(sql, c) { CommandTimeout = 120 };
            foreach (var (n, v) in p) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
            return cmd;
        }
        private async Task<int> ScalarIntAsync(SqlConnection c, string sql, CancellationToken ct, params (string, object?)[] p)
            => Convert.ToInt32(await Cmd(c, sql, p).ExecuteScalarAsync(ct) ?? 0);

        // Close send-backs whose row has since been re-decided.
        private async Task ResolveAsync(SqlConnection c, int batchId, string t, CancellationToken ct)
            => await Cmd(c, $@"UPDATE s SET ResolvedAtUtc=SYSUTCDATETIME()
                               FROM dbo.QaSendBack s JOIN {t} w ON w.VALUATION_KEY=s.ValuationKey
                               WHERE s.BatchId=@b AND s.ResolvedAtUtc IS NULL AND w.Decision IN ('Yes','No')", ("@b", batchId)).ExecuteNonQueryAsync(ct);

        // ---- reads ---------------------------------------------------------
        public async Task<List<QaBatch>> BatchesAsync(CancellationToken ct) =>
            await _db.Batches.AsNoTracking().Where(b => b.CleanWeekTableName != null && b.CleanWeekTableName != "")
                .OrderByDescending(b => b.Id).Take(100).Select(b => new QaBatch { Id = b.Id, Name = b.BatchName }).ToListAsync(ct);

        public async Task<List<QaSectorRow>> SectorsAsync(int batchId, CancellationToken ct)
        {
            var t = await TableAsync(batchId, ct);
            await using var c = await OpenAsync(ct);
            await ResolveAsync(c, batchId, t, ct);
            var list = new List<QaSectorRow>();
            await using (var r = await Cmd(c, $@"SELECT ISNULL(NULLIF(LTRIM(RTRIM(w.SECTOR)),''),'Unknown') Sector, COUNT(*) Total,
                    SUM(CASE WHEN w.Decision='Yes' THEN 1 ELSE 0 END) Y, SUM(CASE WHEN w.Decision='No' THEN 1 ELSE 0 END) N,
                    SUM(CASE WHEN ISNULL(w.Decision,'Pending') NOT IN ('Yes','No') THEN 1 ELSE 0 END) P
                    FROM {t} w GROUP BY ISNULL(NULLIF(LTRIM(RTRIM(w.SECTOR)),''),'Unknown') ORDER BY 1").ExecuteReaderAsync(ct))
                while (await r.ReadAsync(ct))
                    list.Add(new QaSectorRow { Sector = r.GetString(0), Total = r.GetInt32(1), Yes = r.GetInt32(2), No = r.GetInt32(3), Pending = r.GetInt32(4) });

            var appr = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            await using (var r = await Cmd(c, "SELECT Sector, ApprovedBy FROM dbo.QaSectorApproval WHERE BatchId=@b", ("@b", batchId)).ExecuteReaderAsync(ct))
                while (await r.ReadAsync(ct)) appr[r.GetString(0)] = r.GetString(1);
            var ret = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            await using (var r = await Cmd(c, "SELECT ISNULL(Sector,'Unknown'), COUNT(*) FROM dbo.QaSendBack WHERE BatchId=@b AND ResolvedAtUtc IS NULL GROUP BY Sector", ("@b", batchId)).ExecuteReaderAsync(ct))
                while (await r.ReadAsync(ct)) ret[r.GetString(0)] = r.GetInt32(1);
            foreach (var s in list)
            {
                s.Approved = appr.TryGetValue(s.Sector, out var by); s.ApprovedBy = by;
                s.Returned = ret.TryGetValue(s.Sector, out var n) ? n : 0;
            }
            return list;
        }

        // filter: "review" = all No + ~10% sample of Yes | "no" | "yes" | "all" | "pending"
        public async Task<List<QaItem>> ListAsync(int batchId, string sector, string filter, CancellationToken ct)
        {
            var t = await TableAsync(batchId, ct);
            var where = filter switch
            {
                "no" => "w.Decision='No'",
                "yes" => "w.Decision='Yes'",
                "pending" => "ISNULL(w.Decision,'Pending') NOT IN ('Yes','No')",
                "all" => "1=1",
                _ => "(w.Decision='No' OR (w.Decision='Yes' AND ABS(CHECKSUM(w.VALUATION_KEY)) % 10 = 0))"
            };
            await using var c = await OpenAsync(ct);
            var items = new List<QaItem>();
            var sql = $@"SELECT TOP 1000 CAST(w.VALUATION_KEY AS nvarchar(100)), ISNULL(NULLIF(LTRIM(RTRIM(w.SECTOR)),''),'Unknown'),
                  ISNULL(NULLIF(w.PREMISE_ID,''),'-'), ISNULL(NULLIF(w.PROPERTY_DESCRIPTION,''),'-'), ISNULL(w.TOWN_NAME_DESC,''),
                  ISNULL({Eff},'-'), ISNULL(w.Decision,'Pending'), ISNULL(w.DecisionComment,''), ISNULL(w.OWNER_NAME,''),
                  TRY_CONVERT(decimal(18,2), w.MARKET_VALUE), w.DecisionBy, w.DecisionAtUtc,
                  CASE WHEN EXISTS(SELECT 1 FROM dbo.QaSendBack s WHERE s.BatchId=@b AND s.ValuationKey=CAST(w.VALUATION_KEY AS nvarchar(100)) AND s.ResolvedAtUtc IS NULL) THEN 1 ELSE 0 END
                  FROM {t} w WHERE ISNULL(NULLIF(LTRIM(RTRIM(w.SECTOR)),''),'Unknown')=@s AND {where}
                  ORDER BY CASE WHEN w.Decision='No' THEN 0 ELSE 1 END, w.VALUATION_KEY";
            await using var r = await Cmd(c, sql, ("@b", batchId), ("@s", sector)).ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                items.Add(new QaItem
                {
                    Key = r.GetString(0), Sector = r.GetString(1), Premise = r.GetString(2), Description = r.GetString(3), Town = r.GetString(4),
                    Valuer = r.GetString(5), Decision = r.GetString(6), Comment = r.GetString(7), Owner = r.GetString(8),
                    MarketValue = r.IsDBNull(9) ? null : r.GetDecimal(9), DecisionBy = r.IsDBNull(10) ? null : r.GetString(10),
                    DecisionAtUtc = r.IsDBNull(11) ? null : r.GetDateTime(11), Returned = r.GetInt32(12) == 1
                });
            return items;
        }

        // ---- writes --------------------------------------------------------
        public async Task<(bool, string)> SendBackAsync(int batchId, string key, string note, string by, CancellationToken ct)
        {
            note = (note ?? "").Trim();
            if (note.Length < 5) return (false, "Please write a reason (at least 5 characters).");
            var t = await TableAsync(batchId, ct);
            await using var c = await OpenAsync(ct);
            string? valuer = null, sector = null;
            await using (var r = await Cmd(c, $"SELECT {Eff}, ISNULL(NULLIF(LTRIM(RTRIM(w.SECTOR)),''),'Unknown') FROM {t} w WHERE CAST(w.VALUATION_KEY AS nvarchar(100))=@k", ("@k", key)).ExecuteReaderAsync(ct))
            { if (!await r.ReadAsync(ct)) return (false, "Row not found."); valuer = r.IsDBNull(0) ? null : r.GetString(0); sector = r.GetString(1); }

            await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
            async Task X(string sql, params (string, object?)[] p) { var cm = Cmd(c, sql, p); cm.Transaction = tx; await cm.ExecuteNonQueryAsync(ct); }
            await X($@"UPDATE {t} SET Decision='Pending', DecisionAtUtc=NULL, DecisionBy=NULL, UpdatedBy=@u, UpdatedAt=SYSUTCDATETIME()
                       WHERE CAST(VALUATION_KEY AS nvarchar(100))=@k", ("@u", by), ("@k", key));
            await X("INSERT dbo.QaSendBack(BatchId,Sector,ValuationKey,Valuer,Note,SentBy) VALUES(@b,@s,@k,@v,@n,@u)",
                ("@b", batchId), ("@s", sector), ("@k", key), ("@v", valuer), ("@n", note), ("@u", by));
            await X("DELETE dbo.QaSectorApproval WHERE BatchId=@b AND Sector=@s", ("@b", batchId), ("@s", sector));
            await X("DELETE dbo.QaSignoff WHERE BatchId=@b", ("@b", batchId));
            await tx.CommitAsync(ct);
            return (true, "Sent back to the valuer. The sector approval was reset.");
        }

        public async Task<(bool, string)> ApproveSectorAsync(int batchId, string sector, string by, CancellationToken ct)
        {
            var row = (await SectorsAsync(batchId, ct)).FirstOrDefault(s => s.Sector.Equals(sector, StringComparison.OrdinalIgnoreCase));
            if (row == null) return (false, "Sector not found.");
            if (row.Pending > 0) return (false, $"{row.Pending} row(s) are still Pending. Valuers must decide them first.");
            await using var c = await OpenAsync(ct);
            await Cmd(c, @"IF NOT EXISTS(SELECT 1 FROM dbo.QaSectorApproval WHERE BatchId=@b AND Sector=@s)
                           INSERT dbo.QaSectorApproval(BatchId,Sector,ApprovedBy) VALUES(@b,@s,@u)",
                ("@b", batchId), ("@s", row.Sector), ("@u", by)).ExecuteNonQueryAsync(ct);
            return (true, $"{row.Sector} approved.");
        }

        public async Task<(bool, string)> SignOffAsync(int batchId, string by, string? note, CancellationToken ct)
        {
            var secs = await SectorsAsync(batchId, ct);
            if (secs.Count == 0) return (false, "No rows in this batch.");
            var open = secs.Where(s => !s.Approved).Select(s => s.Sector).ToList();
            if (open.Count > 0) return (false, "Waiting for Area Manager approval: " + string.Join(", ", open));
            await using var c = await OpenAsync(ct);
            await Cmd(c, @"IF NOT EXISTS(SELECT 1 FROM dbo.QaSignoff WHERE BatchId=@b)
                           INSERT dbo.QaSignoff(BatchId,SignedBy,Note) VALUES(@b,@u,@n)",
                ("@b", batchId), ("@u", by), ("@n", string.IsNullOrWhiteSpace(note) ? null : note.Trim())).ExecuteNonQueryAsync(ct);
            return (true, "Batch signed off. The Valuation Roll can now be created.");
        }

        public async Task<(bool, string?, DateTime?)> SignoffAsync(int batchId, CancellationToken ct)
        {
            await using var c = await OpenAsync(ct);
            await using var r = await Cmd(c, "SELECT SignedBy, SignedAtUtc FROM dbo.QaSignoff WHERE BatchId=@b", ("@b", batchId)).ExecuteReaderAsync(ct);
            return await r.ReadAsync(ct) ? (true, r.GetString(0), r.GetDateTime(1)) : (false, null, null);
        }
    }
}
