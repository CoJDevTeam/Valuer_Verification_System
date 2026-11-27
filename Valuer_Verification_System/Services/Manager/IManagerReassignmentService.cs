using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Infrastructure.Configuration;
using Valuer_Verification_System.Models.Data;

namespace Valuer_Verification_System.Services.Manager
{
    public interface IManagerReassignmentService
    {
        /// <summary>Valuers who currently hold Pending rows in the batch (with their row count). "Unassigned" = nobody.</summary>
        Task<List<CurrentValuerDto>> GetCurrentValuersAsync(int batchId, CancellationToken ct);

        Task<List<string>> GetSectorsAsync(int batchId, CancellationToken ct);

        /// <summary>Active users from UserManagement, the people a manager can reassign to.</summary>
        Task<List<ActiveValuerDto>> GetActiveValuersAsync(CancellationToken ct);

        Task<List<ReassignRowDto>> ListPendingAsync(int batchId, string? currentValuer, string? sector, int top, CancellationToken ct);

        Task ReassignAsync(int batchId, int extractRowId, string fromValuer, string toValuer, string reason, string managerUser, CancellationToken ct);

        Task<BulkReassignResult> BulkReassignAsync(int batchId, string fromValuer, string toValuer, string reason, string managerUser, CancellationToken ct);
    }

    public sealed class CurrentValuerDto
    {
        public string Username { get; set; } = "";
        public int Pending { get; set; }
    }

    public sealed class ActiveValuerDto
    {
        public string Username { get; set; } = "";
        public string FullName { get; set; } = "";
        public string? Sector { get; set; }
        public string? Position { get; set; }
    }

    public sealed class BulkReassignResult
    {
        public int TotalFound { get; set; }
        public int Moved { get; set; }
        public int SkippedNotPending { get; set; }
    }

    public sealed class ReassignRowDto
    {
        public int ExtractRowId { get; set; }
        public string ValuationKey { get; set; } = "";
        public string? PremiseId { get; set; }
        public string? Sector { get; set; }
        public string? PropertyDescription { get; set; }
        public string? Address { get; set; }
        public string? Category { get; set; }
        public decimal? MarketValue { get; set; }
        public string? Owner { get; set; }
        public string? SchemeName { get; set; }
        public string? UnitNo { get; set; }
        public string? Town { get; set; }
        public string? ExtractValuer { get; set; }   // the valuer name as written in the LIS extract
        public string CurrentValuer { get; set; } = "";
    }

    public sealed class ManagerReassignmentService : IManagerReassignmentService
    {
        private static readonly Regex SafeName = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);
        private const string Unassigned = "Unassigned";

        private readonly VvsDbContext _db;
        private readonly IConfiguration _config;
        private readonly IAppMode _mode;

        public ManagerReassignmentService(VvsDbContext db, IConfiguration config, IAppMode mode)
        {
            _db = db;
            _config = config;
            _mode = mode;
        }

        // ── lists ─────────────────────────────────────────────────────────
        public async Task<List<CurrentValuerDto>> GetCurrentValuersAsync(int batchId, CancellationToken ct)
        {
            var (table, eff) = await WeekAsync(batchId, ct);
            var list = new List<CurrentValuerDto>();

            await using var cmd = await CommandAsync(ct);
            cmd.CommandText = $@"
SELECT v, COUNT(*)
FROM (SELECT ISNULL({eff}, N'{Unassigned}') AS v FROM {table} WHERE [Decision] = N'Pending') x
GROUP BY v
ORDER BY v;";
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct))
                list.Add(new CurrentValuerDto { Username = rd.GetString(0), Pending = rd.GetInt32(1) });

            return list;
        }

        public async Task<List<string>> GetSectorsAsync(int batchId, CancellationToken ct)
        {
            var (table, _) = await WeekAsync(batchId, ct);
            var list = new List<string>();

            await using var cmd = await CommandAsync(ct);
            cmd.CommandText = $@"
SELECT DISTINCT ISNULL(NULLIF(LTRIM(RTRIM([SECTOR])), N''), N'Unknown') AS s
FROM {table} ORDER BY s;";
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct)) list.Add(rd.GetString(0));
            return list;
        }

        public async Task<List<ActiveValuerDto>> GetActiveValuersAsync(CancellationToken ct)
        {
            var list = new List<ActiveValuerDto>();
            var cs = _config.GetConnectionString("UserManagement");
            if (string.IsNullOrWhiteSpace(cs)) return list;

            await using var conn = new SqlConnection(cs);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(@"
SELECT LTRIM(RTRIM(Username)), FirstName, SecondName, Surname, Sector, Position
FROM [UserManagement].[dbo].[Users]
WHERE ISNULL(Active, 1) = 1 AND LTRIM(RTRIM(ISNULL(Username, ''))) <> ''
ORDER BY FirstName, Surname;", conn);

            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct))
            {
                string? S(int i) => rd.IsDBNull(i) ? null : rd.GetValue(i)?.ToString()?.Trim();

                var name = string.Join(" ", new[] { S(1), S(2), S(3) }.Where(x => !string.IsNullOrWhiteSpace(x)));
                list.Add(new ActiveValuerDto
                {
                    Username = S(0) ?? "",
                    FullName = string.IsNullOrWhiteSpace(name) ? (S(0) ?? "") : name,
                    Sector = S(4),
                    Position = S(5)
                });
            }

            // Development / Testing: the developer and testers must always be selectable.
            foreach (var u in _mode.AllocateUsernames)
                if (!list.Any(x => string.Equals(x.Username, u, StringComparison.OrdinalIgnoreCase)))
                    list.Insert(0, new ActiveValuerDto { Username = u, FullName = u, Position = _mode.Label });

            return list;
        }

        public async Task<List<ReassignRowDto>> ListPendingAsync(
            int batchId, string? currentValuer, string? sector, int top, CancellationToken ct)
        {
            if (top <= 0) top = 500;
            if (top > 2000) top = 2000;

            var (table, eff) = await WeekAsync(batchId, ct);
            currentValuer = currentValuer?.Trim();

            var where = "[Decision] = N'Pending'";
            if (!string.IsNullOrWhiteSpace(currentValuer))
                where += string.Equals(currentValuer, Unassigned, StringComparison.OrdinalIgnoreCase)
                    ? $" AND {eff} IS NULL"
                    : $" AND {eff} = @v";
            if (!string.IsNullOrWhiteSpace(sector))
                where += " AND ISNULL(NULLIF(LTRIM(RTRIM([SECTOR])), N''), N'Unknown') = @s";

            await using var cmd = await CommandAsync(ct);
            cmd.CommandText = $@"
SELECT TOP (@top)
       [ExtractRowId],
       ISNULL({eff}, N'{Unassigned}') AS CurrentValuer,
       [VALUATION_KEY],
       [SECTOR],
       COALESCE(NULLIF(LTRIM(RTRIM([PREMISE_ID])), N''), NULLIF(LTRIM(RTRIM([RIGHTS_PREMISE])), N'')) AS PremiseId,
       COALESCE(
           NULLIF(LTRIM(RTRIM([PROPERTY_DESCRIPTION])), N''),
           NULLIF(LTRIM(RTRIM([PROPERTY_DESC])), N''),
           NULLIF(LTRIM(RTRIM([ST_PROPERTY_DESCRIPTION])), N''),
           NULLIF(LTRIM(RTRIM(
               ISNULL(NULLIF(LTRIM(RTRIM([RE])), N'') + N' ', N'') +
               ISNULL(N'PORTION ' + NULLIF(LTRIM(RTRIM(CONVERT(NVARCHAR(50), TRY_CONVERT(INT, [PTN])))), N'0') + N' ', N'') +
               ISNULL(N'ERF ' + NULLIF(LTRIM(RTRIM(CONVERT(NVARCHAR(50), TRY_CONVERT(INT, [ERF])))), N'0') + N' ', N'') +
               ISNULL([TOWN_NAME_DESC], N''))), N''),
           NULLIF(LTRIM(RTRIM([SCHEME_NAME_DESC])), N'')) AS PropDesc,
       COALESCE(NULLIF(LTRIM(RTRIM([PREMISE_ADDRESS])), N''), NULLIF(LTRIM(RTRIM([PHYSICAL_ADDRESS])), N''), [LIS_STREET_ADDRESS]) AS Addr,
       ISNULL(NULLIF(LTRIM(RTRIM([CAT_DESC])), N''), [PROPERTY_CATEGORY]) AS Cat,
       [MARKET_VALUE],
       [OWNER_NAME],
       [SCHEME_NAME_DESC],
       [UNIT_NO],
       [TOWN_NAME_DESC],
       [Valuer]
FROM {table}
WHERE {where}
ORDER BY CurrentValuer, [VALUATION_KEY];";
            Add(cmd, "@top", top);
            if (cmd.CommandText.Contains("@v")) Add(cmd, "@v", currentValuer!);
            if (cmd.CommandText.Contains("@s")) Add(cmd, "@s", sector!);

            var rows = new List<ReassignRowDto>();
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct))
            {
                string? S(int i) => rd.IsDBNull(i) ? null : Convert.ToString(rd.GetValue(i))?.Trim();

                decimal? mv = null;
                if (!rd.IsDBNull(8))
                    try { mv = Convert.ToDecimal(rd.GetValue(8)); } catch { }

                rows.Add(new ReassignRowDto
                {
                    ExtractRowId = rd.GetInt32(0),
                    CurrentValuer = S(1) ?? Unassigned,
                    ValuationKey = S(2) ?? "",
                    Sector = S(3),
                    PremiseId = S(4),
                    PropertyDescription = S(5),
                    Address = S(6),
                    Category = S(7),
                    MarketValue = mv,
                    Owner = S(9),
                    SchemeName = S(10),
                    UnitNo = S(11),
                    Town = S(12),
                    ExtractValuer = S(13)
                });
            }

            return rows;
        }

        // ── single reassign ───────────────────────────────────────────────
        public async Task ReassignAsync(
            int batchId, int extractRowId, string fromValuer, string toValuer,
            string reason, string managerUser, CancellationToken ct)
        {
            fromValuer = (fromValuer ?? "").Trim();
            toValuer = (toValuer ?? "").Trim();
            reason = (reason ?? "").Trim();
            managerUser = (managerUser ?? "manager").Trim();

            ValidateMove(fromValuer, toValuer, reason);

            var moved = await MoveAsync(batchId, new[] { extractRowId }, fromValuer, toValuer, managerUser, ct);

            if (moved.Count == 0)
                throw new InvalidOperationException(
                    "Nothing was changed. The record is already verified, or its valuer changed. Refresh the page and try again.");

            await WriteHistoryAsync(moved, fromValuer, toValuer, reason, managerUser, ct);
        }

        // ── bulk reassign ─────────────────────────────────────────────────
        public async Task<BulkReassignResult> BulkReassignAsync(
            int batchId, string fromValuer, string toValuer, string reason, string managerUser, CancellationToken ct)
        {
            fromValuer = (fromValuer ?? "").Trim();
            toValuer = (toValuer ?? "").Trim();
            reason = (reason ?? "").Trim();
            managerUser = (managerUser ?? "manager").Trim();

            if (batchId <= 0) throw new InvalidOperationException("Batch is required.");
            if (string.IsNullOrWhiteSpace(fromValuer)) throw new InvalidOperationException("From valuer is required.");
            ValidateMove(fromValuer, toValuer, reason);

            var (table, eff) = await WeekAsync(batchId, ct);

            // how many rows does the source valuer hold in total (any decision)?
            int found;
            await using (var cmd = await CommandAsync(ct))
            {
                var cond = FromCondition(fromValuer, eff);
                cmd.CommandText = $"SELECT COUNT(*) FROM {table} WHERE {cond};";
                if (cond.Contains("@from")) Add(cmd, "@from", fromValuer);
                found = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
            }

            var moved = await MoveAsync(batchId, null, fromValuer, toValuer, managerUser, ct);

            if (moved.Count > 0)
                await WriteHistoryAsync(moved, fromValuer, toValuer, reason, managerUser, ct);

            return new BulkReassignResult
            {
                TotalFound = found,
                Moved = moved.Count,
                SkippedNotPending = Math.Max(0, found - moved.Count)
            };
        }

        // ── helpers ───────────────────────────────────────────────────────
        private static void ValidateMove(string from, string to, string reason)
        {
            if (string.IsNullOrWhiteSpace(to))
                throw new InvalidOperationException("Please select a new valuer.");
            if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("New valuer must be different from current valuer.");
            if (string.IsNullOrWhiteSpace(reason))
                throw new InvalidOperationException("Reason is required.");
        }

        private static string FromCondition(string from, string eff)
            => string.Equals(from, Unassigned, StringComparison.OrdinalIgnoreCase)
                ? $"{eff} IS NULL"
                : $"{eff} = @from";

        /// <summary>Moves Pending rows to the new valuer inside the week table. Returns the row ids that moved.</summary>
        private async Task<List<int>> MoveAsync(
            int batchId, IReadOnlyCollection<int>? rowIds, string from, string to, string manager, CancellationToken ct)
        {
            var (table, eff) = await WeekAsync(batchId, ct);
            var hasDev = await HasDevColumnAsync(table, ct);

            var cond = FromCondition(from, eff);
            var idFilter = rowIds is { Count: > 0 }
                ? " AND [ExtractRowId] IN (" + string.Join(",", rowIds.Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture))) + ")"
                : "";

            // A row that was handed out for testing keeps working through DevAllocatedTo, so move that too.
            var devSet = hasDev
                ? ", [DevAllocatedTo] = CASE WHEN NULLIF(LTRIM(RTRIM([DevAllocatedTo])), N'') IS NULL THEN [DevAllocatedTo] ELSE @to END"
                : "";

            await using var cmd = await CommandAsync(ct);
            cmd.CommandText = $@"
UPDATE {table}
   SET [Username] = @to{devSet},
       [UpdatedBy] = @mgr,
       [UpdatedAt] = SYSUTCDATETIME()
OUTPUT inserted.[ExtractRowId]
 WHERE [Decision] = N'Pending' AND {cond}{idFilter};";
            Add(cmd, "@to", to);
            Add(cmd, "@mgr", manager);
            if (cond.Contains("@from")) Add(cmd, "@from", from);

            var moved = new List<int>();
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct)) moved.Add(rd.GetInt32(0));
            return moved;
        }

        /// <summary>Audit trail (Assignments + AssignmentHistories) keeps working: the raw ExtractRows row still exists.</summary>
        private async Task WriteHistoryAsync(
            List<int> rowIds, string from, string to, string reason, string manager, CancellationToken ct)
        {
            var now = DateTime.UtcNow;

            var existing = await _db.Assignments
                .Where(a => rowIds.Contains(a.ExtractRowId))
                .ToDictionaryAsync(a => a.ExtractRowId, ct);

            foreach (var id in rowIds)
            {
                if (existing.TryGetValue(id, out var a))
                {
                    a.AssignedToValuer = to;
                    a.AssignedAtUtc = now;
                    a.AssignedBy = manager;
                    a.UpdatedAt = now;
                    a.UpdatedBy = manager;
                }
                else
                {
                    var na = new Assignment
                    {
                        ExtractRowId = id,
                        AssignedToValuer = to,
                        AssignedAtUtc = now,
                        AssignedBy = manager,
                        CreatedBy = manager,
                        CreatedAt = now
                    };
                    _db.Assignments.Add(na);
                    existing[id] = na;
                }
            }

            await _db.SaveChangesAsync(ct);

            foreach (var id in rowIds)
            {
                _db.AssignmentHistories.Add(new AssignmentHistory
                {
                    AssignmentId = existing[id].Id,
                    FromValuer = from,
                    ToValuer = to,
                    ChangedBy = manager,
                    ChangedAtUtc = now,
                    Reason = reason,
                    CreatedBy = manager,
                    CreatedAt = now
                });
            }

            await _db.SaveChangesAsync(ct);
        }

        /// <summary>The week table of a batch and the SQL expression for "who holds this row now".</summary>
        private async Task<(string table, string eff)> WeekAsync(int batchId, CancellationToken ct)
        {
            var name = await _db.Batches.AsNoTracking()
                .Where(b => b.Id == batchId)
                .Select(b => b.CleanWeekTableName)
                .FirstOrDefaultAsync(ct);

            if (string.IsNullOrWhiteSpace(name) || !SafeName.IsMatch(name))
                throw new InvalidOperationException("This batch has no week table yet. Clean the extract first.");

            var table = $"[SECTION_78_NOTICE].[dbo].[{name}]";
            var eff = await HasDevColumnAsync(table, ct)
                ? "ISNULL(NULLIF(LTRIM(RTRIM([DevAllocatedTo])), N''), NULLIF(LTRIM(RTRIM([Username])), N''))"
                : "NULLIF(LTRIM(RTRIM([Username])), N'')";

            return (table, eff);
        }

        private async Task<bool> HasDevColumnAsync(string table, CancellationToken ct)
        {
            await using var cmd = await CommandAsync(ct);
            cmd.CommandText = "SELECT CASE WHEN EXISTS (SELECT 1 FROM [SECTION_78_NOTICE].sys.columns WHERE object_id = OBJECT_ID(@t) AND name = N'DevAllocatedTo') THEN 1 ELSE 0 END;";
            Add(cmd, "@t", table);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) == 1;
        }

        private async Task<DbCommand> CommandAsync(CancellationToken ct)
        {
            var conn = _db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync(ct);
            var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 120;
            return cmd;
        }

        private static void Add(DbCommand cmd, string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            cmd.Parameters.Add(p);
        }
    }
}
