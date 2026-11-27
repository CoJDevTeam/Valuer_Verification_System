using System.Collections.Concurrent;
using System.Data.Common;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Models.Data;

namespace Valuer_Verification_System.Services.Valuer
{
    public sealed class WeekCounts
    {
        public int Total { get; set; }
        public int Pending { get; set; }
        public int Yes { get; set; }
        public int No { get; set; }
        public int YesToday { get; set; }
        public int NoToday { get; set; }
    }

    /// <summary>
    /// The valuer's window on the week table (the only working table).
    /// - A valuer only sees batches the Project Manager has APPROVED.
    /// - The valuer of a row is COALESCE(DevAllocatedTo, Username), so test allocations work.
    /// - Rows come back as ExtractRow objects so the existing screens keep working.
    /// - Decisions are written straight into the week table.
    /// </summary>
    public interface IWeekRowStore
    {
        /// <summary>Batches that have a week table and were approved by the Project Manager (newest first).</summary>
        Task<List<Batch>> ApprovedBatchesAsync(CancellationToken ct);

        /// <summary>The valuer's rows. batchId null = all approved batches. decision null = all decisions.</summary>
        Task<List<ExtractRow>> LoadForValuerAsync(string valuer, int? batchId, string? decision, CancellationToken ct);

        /// <summary>One row, only if it belongs to the valuer.</summary>
        Task<ExtractRow?> GetRowAsync(int rowId, string valuer, CancellationToken ct);

        /// <summary>All rows of a property (same VALUATION_KEY) in a batch: split / multipurpose lines.</summary>
        Task<List<ExtractRow>> GetSiblingsAsync(int batchId, string valuationKey, CancellationToken ct);

        Task<WeekCounts> CountsAsync(string valuer, int batchId, CancellationToken ct);

        /// <summary>Sets the decision on rows owned by the valuer. Returns how many rows changed.</summary>
        Task<int> SetDecisionAsync(IReadOnlyCollection<int> rowIds, int? batchId, string valuer,
                                   string decision, string? comment, CancellationToken ct);
    }

    public sealed class WeekRowStore : IWeekRowStore
    {
        private static readonly Regex SafeName = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);
        private static readonly ConcurrentDictionary<string, bool> HasDevColumn = new();

        // week-table column -> ExtractRow property (match ignoring case and underscores)
        private static readonly Dictionary<string, PropertyInfo> Props = typeof(ExtractRow)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.Name != "Id" && p.Name != "Decision"
                        && p.Name != "Batch" && p.Name != "Assignment")
            .ToDictionary(p => p.Name.ToUpperInvariant(), p => p);

        private readonly VvsDbContext _db;
        public WeekRowStore(VvsDbContext db) => _db = db;

        // ── approved batches ──────────────────────────────────────────────
        public Task<List<Batch>> ApprovedBatchesAsync(CancellationToken ct)
            => _db.Batches
                .AsNoTracking()
                .Include(b => b.WeekWindow)
                .Where(b => b.CleanWeekTableName != null && b.CleanWeekTableName != ""
                         && b.ProjectManagerApprovedAtUtc != null)
                .OrderByDescending(b => b.Id)
                .Take(60)
                .ToListAsync(ct);

        private async Task<List<Batch>> TargetBatchesAsync(int? batchId, CancellationToken ct)
        {
            var all = await ApprovedBatchesAsync(ct);
            return batchId.HasValue ? all.Where(b => b.Id == batchId.Value).ToList() : all;
        }

        // ── reads ─────────────────────────────────────────────────────────
        public async Task<List<ExtractRow>> LoadForValuerAsync(
            string valuer, int? batchId, string? decision, CancellationToken ct)
        {
            valuer = (valuer ?? "").Trim();
            var result = new List<ExtractRow>();
            if (valuer.Length == 0) return result;

            foreach (var b in await TargetBatchesAsync(batchId, ct))
            {
                var table = Table(b);
                var eff = await EffectiveValuerAsync(table, ct);

                var sql = $"SELECT TOP (5000) * FROM {table} WHERE {eff} = @v";
                if (!string.IsNullOrWhiteSpace(decision)) sql += " AND [Decision] = @d";
                sql += " ORDER BY [SECTOR], [VALUATION_KEY], [ExtractRowId]";

                result.AddRange(await ReadAsync(sql, valuer, decision, null, ct));
            }

            return result;
        }

        public async Task<ExtractRow?> GetRowAsync(int rowId, string valuer, CancellationToken ct)
        {
            valuer = (valuer ?? "").Trim();

            foreach (var b in await TargetBatchesAsync(null, ct))
            {
                var table = Table(b);
                var eff = await EffectiveValuerAsync(table, ct);

                var rows = await ReadAsync(
                    $"SELECT * FROM {table} WHERE [ExtractRowId] = @id AND {eff} = @v",
                    valuer, null, rowId, ct);

                if (rows.Count > 0) return rows[0];
            }

            return null;
        }

        public async Task<List<ExtractRow>> GetSiblingsAsync(int batchId, string valuationKey, CancellationToken ct)
        {
            var b = (await TargetBatchesAsync(batchId, ct)).FirstOrDefault();
            if (b is null) return new List<ExtractRow>();

            return await ReadAsync(
                $"SELECT * FROM {Table(b)} WHERE [VALUATION_KEY] = @k ORDER BY [VALUATION_SPLIT_NO], [ExtractRowId]",
                null, null, null, ct, key: valuationKey);
        }

        public async Task<WeekCounts> CountsAsync(string valuer, int batchId, CancellationToken ct)
        {
            var c = new WeekCounts();
            valuer = (valuer ?? "").Trim();

            var b = (await TargetBatchesAsync(batchId, ct)).FirstOrDefault();
            if (b is null || valuer.Length == 0) return c;

            var table = Table(b);
            var eff = await EffectiveValuerAsync(table, ct);

            var conn = await OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
SELECT COUNT(*),
       ISNULL(SUM(CASE WHEN [Decision] = 'Pending' THEN 1 ELSE 0 END), 0),
       ISNULL(SUM(CASE WHEN [Decision] = 'Yes' THEN 1 ELSE 0 END), 0),
       ISNULL(SUM(CASE WHEN [Decision] = 'No' THEN 1 ELSE 0 END), 0),
       ISNULL(SUM(CASE WHEN [Decision] = 'Yes' AND [DecisionAtUtc] >= @today THEN 1 ELSE 0 END), 0),
       ISNULL(SUM(CASE WHEN [Decision] = 'No'  AND [DecisionAtUtc] >= @today THEN 1 ELSE 0 END), 0)
FROM {table} WHERE {eff} = @v";
            AddParam(cmd, "@v", valuer);
            AddParam(cmd, "@today", DateTime.UtcNow.Date);

            await using var rd = await cmd.ExecuteReaderAsync(ct);
            if (await rd.ReadAsync(ct))
            {
                c.Total = rd.GetInt32(0);
                c.Pending = rd.GetInt32(1);
                c.Yes = rd.GetInt32(2);
                c.No = rd.GetInt32(3);
                c.YesToday = rd.GetInt32(4);
                c.NoToday = rd.GetInt32(5);
            }

            return c;
        }

        // ── write ─────────────────────────────────────────────────────────
        public async Task<int> SetDecisionAsync(
            IReadOnlyCollection<int> rowIds, int? batchId, string valuer,
            string decision, string? comment, CancellationToken ct)
        {
            valuer = (valuer ?? "").Trim();
            if (rowIds.Count == 0 || valuer.Length == 0) return 0;
            if (decision is not ("Yes" or "No" or "Pending"))
                throw new InvalidOperationException("Invalid verification decision.");

            var ids = string.Join(",", rowIds.Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            var changed = 0;

            foreach (var b in await TargetBatchesAsync(batchId, ct))
            {
                var table = Table(b);
                var eff = await EffectiveValuerAsync(table, ct);

                var conn = await OpenAsync(ct);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $@"
UPDATE {table}
   SET [Decision] = @d,
       [DecisionAtUtc] = CASE WHEN @d = 'Pending' THEN NULL ELSE SYSUTCDATETIME() END,
       [DecisionBy] = CASE WHEN @d = 'Pending' THEN NULL ELSE @v END,
       [DecisionComment] = @c,
       [UpdatedBy] = @v,
       [UpdatedAt] = SYSUTCDATETIME()
 WHERE {eff} = @v AND [ExtractRowId] IN ({ids});";
                AddParam(cmd, "@d", decision);
                AddParam(cmd, "@v", valuer);
                AddParam(cmd, "@c", string.IsNullOrWhiteSpace(comment) ? DBNull.Value : comment.Trim());

                changed += await cmd.ExecuteNonQueryAsync(ct);
            }

            return changed;
        }

        // ── helpers ───────────────────────────────────────────────────────
        private static string Table(Batch b)
        {
            var name = b.CleanWeekTableName ?? "";
            if (!SafeName.IsMatch(name))
                throw new InvalidOperationException("Invalid week table name.");
            return $"[SECTION_78_NOTICE].[dbo].[{name}]";
        }

        /// <summary>The valuer of a row: the test allocation if there is one, else the real username.</summary>
        private async Task<string> EffectiveValuerAsync(string table, CancellationToken ct)
        {
            var has = HasDevColumn.TryGetValue(table, out var cached) && cached;
            if (!has)
            {
                var conn = await OpenAsync(ct);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT CASE WHEN COL_LENGTH(@t, 'DevAllocatedTo') IS NULL THEN 0 ELSE 1 END";
                AddParam(cmd, "@t", table);
                has = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) == 1;
                if (has) HasDevColumn[table] = true;
            }

            return has
                ? "ISNULL(NULLIF(LTRIM(RTRIM([DevAllocatedTo])), ''), LTRIM(RTRIM([Username])))"
                : "LTRIM(RTRIM([Username]))";
        }

        private async Task<DbConnection> OpenAsync(CancellationToken ct)
        {
            var conn = _db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync(ct);
            return conn;
        }

        private static void AddParam(DbCommand cmd, string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            cmd.Parameters.Add(p);
        }

        private async Task<List<ExtractRow>> ReadAsync(
            string sql, string? valuer, string? decision, int? rowId, CancellationToken ct, string? key = null)
        {
            var conn = await OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = 120;
            if (valuer != null) AddParam(cmd, "@v", valuer);
            if (decision != null) AddParam(cmd, "@d", decision);
            if (rowId.HasValue) AddParam(cmd, "@id", rowId.Value);
            if (key != null) AddParam(cmd, "@k", key);

            var rows = new List<ExtractRow>();
            await using var rd = await cmd.ExecuteReaderAsync(ct);

            // map columns once
            var map = new PropertyInfo?[rd.FieldCount];
            int idCol = -1, decCol = -1;
            for (var i = 0; i < rd.FieldCount; i++)
            {
                var col = rd.GetName(i);
                if (col.Equals("ExtractRowId", StringComparison.OrdinalIgnoreCase)) { idCol = i; continue; }
                if (col.Equals("Decision", StringComparison.OrdinalIgnoreCase)) { decCol = i; continue; }
                if (col.EndsWith("_") || col.Equals("OwnerS_Name", StringComparison.OrdinalIgnoreCase)) continue;

                var norm = col.Replace("_", "").ToUpperInvariant();
                if (Props.TryGetValue(norm, out var p)) map[i] = p;
            }

            while (await rd.ReadAsync(ct))
            {
                var r = new ExtractRow();
                if (idCol >= 0 && !rd.IsDBNull(idCol)) r.Id = Convert.ToInt32(rd.GetValue(idCol));

                if (decCol >= 0 && !rd.IsDBNull(decCol))
                {
                    var d = Convert.ToString(rd.GetValue(decCol))?.Trim();
                    r.Decision = string.Equals(d, "Yes", StringComparison.OrdinalIgnoreCase) ? VerificationDecision.Yes
                               : string.Equals(d, "No", StringComparison.OrdinalIgnoreCase) ? VerificationDecision.No
                               : VerificationDecision.Pending;
                }

                for (var i = 0; i < map.Length; i++)
                {
                    var p = map[i];
                    if (p is null || rd.IsDBNull(i)) continue;
                    try { p.SetValue(r, ConvertValue(rd.GetValue(i), p.PropertyType)); }
                    catch { /* a column that does not convert is left empty */ }
                }

                rows.Add(r);
            }

            return rows;
        }

        private static object? ConvertValue(object value, Type target)
        {
            var t = Nullable.GetUnderlyingType(target) ?? target;

            if (t == typeof(string)) return System.Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            if (t == typeof(DateOnly))
                return value switch
                {
                    DateTime dt => DateOnly.FromDateTime(dt),
                    DateOnly d => d,
                    _ => DateOnly.Parse(System.Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!)
                };
            if (t == typeof(DateTime)) return System.Convert.ToDateTime(value);
            if (t == typeof(decimal)) return System.Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture);
            if (t == typeof(int)) return System.Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
            if (t == typeof(bool)) return System.Convert.ToBoolean(value);

            return System.Convert.ChangeType(value, t, System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
