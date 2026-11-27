using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Valuer_Verification_System.Data;

namespace Valuer_Verification_System.Services.Roll
{
    public interface IRollPreparationService
    {
        Task<List<RollBatchOption>> BatchesAsync(CancellationToken ct);
        Task<List<RollPrepRow>> ListAsync(CancellationToken ct);
        Task<int> CreateAsync(RollCreateInput input, string user, CancellationToken ct);
        Task<RollDetail?> GetAsync(int id, string? propertyType, string? source, string? decision, CancellationToken ct);
        Task AddRelatedAsync(int id, RollRelatedInput input, string user, CancellationToken ct);
        Task RemoveRelatedAsync(int id, int entryId, CancellationToken ct);
        Task SubmitAsync(int id, string user, CancellationToken ct);
        Task<int> DecideAsync(int id, IReadOnlyCollection<int> entryIds, string decision, string? comment, string user, CancellationToken ct);
        Task ConfirmAsync(int id, string user, CancellationToken ct);
        Task<byte[]> ExportCsvAsync(int id, CancellationToken ct);
    }

    public sealed class RollBatchOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public DateOnly Date { get; set; }
    }

    public sealed class RollCreateInput
    {
        public int BatchId { get; set; }
        public string RollType { get; set; } = "";
        public string RollNumber { get; set; } = "";
        public string GV { get; set; } = "";
        public string RollPeriod { get; set; } = "";
    }

    public sealed class RollRelatedInput
    {
        public string PremiseId { get; set; } = "";
        public string Source { get; set; } = "Review";      // Review | Amendment
        public string? ValuationKey { get; set; }
        public string? PropertyDescription { get; set; }
        public decimal? MarketValue { get; set; }
        public string? Note { get; set; }
    }

    public sealed class RollPrepRow
    {
        public int Id { get; set; }
        public int BatchId { get; set; }
        public string BatchName { get; set; } = "";
        public string RollType { get; set; } = "";
        public string RollNumber { get; set; } = "";
        public string GV { get; set; } = "";
        public string RollPeriod { get; set; } = "";
        public string Status { get; set; } = "";
        public int Entries { get; set; }
        public int Pending { get; set; }
        public int Included { get; set; }
        public int Excluded { get; set; }
        public string CreatedBy { get; set; } = "";
        public DateTime CreatedAtUtc { get; set; }
    }

    public sealed class RollEntryRow
    {
        public int Id { get; set; }
        public int? ExtractRowId { get; set; }
        public string? ValuationKey { get; set; }
        public string? PremiseId { get; set; }
        public string PropertyType { get; set; } = "FH";
        public string? Sector { get; set; }
        public string? PropertyDescription { get; set; }
        public string? Address { get; set; }
        public string? Category { get; set; }
        public decimal? MarketValue { get; set; }
        public string Source { get; set; } = "Supplementary";
        public int? RelatedToEntryId { get; set; }
        public string? Note { get; set; }
        public string Decision { get; set; } = "Pending";
        public string? DecidedBy { get; set; }
        public DateTime? DecidedAtUtc { get; set; }
        public string? DecisionComment { get; set; }
    }

    public sealed class RollDetail
    {
        public RollPrepRow Header { get; set; } = new();
        public List<RollEntryRow> Entries { get; set; } = new();
        public int UnverifiedInBatch { get; set; }
        public string? SubmittedBy { get; set; }
        public DateTime? SubmittedAtUtc { get; set; }
        public string? ConfirmedBy { get; set; }
        public DateTime? ConfirmedAtUtc { get; set; }
    }

    public sealed class RollPreparationService : IRollPreparationService
    {
        private static readonly Regex SafeName = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);
        private readonly VvsDbContext _db;

        public RollPreparationService(VvsDbContext db) => _db = db;

        // ── lists ─────────────────────────────────────────────────────────
        public async Task<List<RollBatchOption>> BatchesAsync(CancellationToken ct)
        {
            var rows = await _db.Batches.AsNoTracking()
                .Where(b => b.CleanWeekTableName != null && b.CleanWeekTableName != "")
                .OrderByDescending(b => b.Id)
                .Take(100)
                .Select(b => new { b.Id, b.BatchName, b.BatchDate })
                .ToListAsync(ct);

            return rows.Select(b => new RollBatchOption { Id = b.Id, Name = b.BatchName, Date = b.BatchDate }).ToList();
        }

        public async Task<List<RollPrepRow>> ListAsync(CancellationToken ct)
        {
            var list = new List<RollPrepRow>();
            await using var cmd = await CommandAsync(ct);
            cmd.CommandText = HeaderSql + " ORDER BY p.Id DESC;";
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct)) list.Add(ReadHeader(rd));
            return list;
        }

        // ── Step 1: identify the supplementary entries flagged Yes ────────
        public async Task<int> CreateAsync(RollCreateInput input, string user, CancellationToken ct)
        {
            if (input.BatchId <= 0) throw new InvalidOperationException("Please select a batch.");
            if (string.IsNullOrWhiteSpace(input.RollType)) throw new InvalidOperationException("Roll type description is required.");
            if (string.IsNullOrWhiteSpace(input.RollNumber)) throw new InvalidOperationException("Roll number is required.");
            if (string.IsNullOrWhiteSpace(input.GV)) throw new InvalidOperationException("GV is required.");
            if (string.IsNullOrWhiteSpace(input.RollPeriod)) throw new InvalidOperationException("Valuation roll period is required.");

            var table = await WeekTableAsync(input.BatchId, ct);

            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            int id;

            await using (var cmd = await CommandAsync(ct))
            {
                cmd.Transaction = tx.GetDbTransaction();
                cmd.CommandText = @"
INSERT dbo.RollPreparations (BatchId, RollType, RollNumber, GV, RollPeriod, CreatedBy)
OUTPUT inserted.Id
VALUES (@b, @rt, @rn, @gv, @rp, @u);";
                Add(cmd, "@b", input.BatchId);
                Add(cmd, "@rt", input.RollType.Trim());
                Add(cmd, "@rn", input.RollNumber.Trim());
                Add(cmd, "@gv", input.GV.Trim());
                Add(cmd, "@rp", input.RollPeriod.Trim());
                Add(cmd, "@u", user);
                id = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
            }

            int added;
            await using (var cmd = await CommandAsync(ct))
            {
                cmd.Transaction = tx.GetDbTransaction();
                // BR-01: only entries the valuers marked Yes. Sectional title = has a scheme or unit, otherwise freehold.
                cmd.CommandText = $@"
INSERT dbo.RollEntries
    (RollPreparationId, ExtractRowId, ValuationKey, PremiseId, PropertyType, Sector,
     PropertyDescription, Address, Category, MarketValue, Source)
SELECT @id, [ExtractRowId], [VALUATION_KEY],
       COALESCE(NULLIF(LTRIM(RTRIM([PREMISE_ID])), N''), NULLIF(LTRIM(RTRIM([RIGHTS_PREMISE])), N'')),
       CASE WHEN NULLIF(LTRIM(RTRIM([SCHEME_NAME_DESC])), N'') IS NOT NULL
              OR NULLIF(LTRIM(RTRIM([UNIT_NO])), N'') IS NOT NULL THEN N'ST' ELSE N'FH' END,
       [SECTOR],
       COALESCE(NULLIF(LTRIM(RTRIM([PROPERTY_DESCRIPTION])), N''), NULLIF(LTRIM(RTRIM([PROPERTY_DESC])), N''),
                NULLIF(LTRIM(RTRIM([SCHEME_NAME_DESC])), N'')),
       COALESCE(NULLIF(LTRIM(RTRIM([PREMISE_ADDRESS])), N''), NULLIF(LTRIM(RTRIM([PHYSICAL_ADDRESS])), N''),
                NULLIF(LTRIM(RTRIM([LIS_STREET_ADDRESS])), N'')),
       COALESCE(NULLIF(LTRIM(RTRIM([CAT_DESC])), N''), NULLIF(LTRIM(RTRIM([PROPERTY_CATEGORY])), N'')),
       TRY_CONVERT(DECIMAL(18,2), [MARKET_VALUE]),
       N'Supplementary'
FROM {table}
WHERE [Decision] = N'Yes';
SELECT @@ROWCOUNT;";
                Add(cmd, "@id", id);
                added = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
            }

            if (added == 0)
                throw new InvalidOperationException("This batch has no properties marked Yes yet. Nothing to put on the roll.");

            await tx.CommitAsync(ct);
            return id;
        }

        // ── detail ────────────────────────────────────────────────────────
        public async Task<RollDetail?> GetAsync(int id, string? propertyType, string? source, string? decision, CancellationToken ct)
        {
            RollDetail? d = null;

            await using (var cmd = await CommandAsync(ct))
            {
                cmd.CommandText = HeaderSql + " WHERE p.Id = @id;";
                Add(cmd, "@id", id);
                await using var rd = await cmd.ExecuteReaderAsync(ct);
                if (await rd.ReadAsync(ct))
                {
                    d = new RollDetail { Header = ReadHeader(rd) };
                    d.SubmittedBy = rd.IsDBNull(rd.GetOrdinal("SubmittedBy")) ? null : rd.GetString(rd.GetOrdinal("SubmittedBy"));
                    d.SubmittedAtUtc = rd.IsDBNull(rd.GetOrdinal("SubmittedAtUtc")) ? null : rd.GetDateTime(rd.GetOrdinal("SubmittedAtUtc"));
                    d.ConfirmedBy = rd.IsDBNull(rd.GetOrdinal("ConfirmedBy")) ? null : rd.GetString(rd.GetOrdinal("ConfirmedBy"));
                    d.ConfirmedAtUtc = rd.IsDBNull(rd.GetOrdinal("ConfirmedAtUtc")) ? null : rd.GetDateTime(rd.GetOrdinal("ConfirmedAtUtc"));
                }
            }
            if (d == null) return null;

            var where = "RollPreparationId = @id";
            if (!string.IsNullOrWhiteSpace(propertyType)) where += " AND PropertyType = @pt";
            if (!string.IsNullOrWhiteSpace(source)) where += " AND Source = @src";
            if (!string.IsNullOrWhiteSpace(decision)) where += " AND Decision = @dec";

            await using (var cmd = await CommandAsync(ct))
            {
                // Group each review / amendment under its supplementary entry (same premise), then by premise.
                cmd.CommandText = $@"
SELECT Id, ExtractRowId, ValuationKey, PremiseId, PropertyType, Sector, PropertyDescription, Address, Category,
       MarketValue, Source, RelatedToEntryId, Note, Decision, DecidedBy, DecidedAtUtc, DecisionComment
FROM dbo.RollEntries
WHERE {where}
ORDER BY PropertyType, ISNULL(PremiseId, N'~'), CASE Source WHEN N'Supplementary' THEN 0 ELSE 1 END, Id;";
                Add(cmd, "@id", id);
                if (where.Contains("@pt")) Add(cmd, "@pt", propertyType!);
                if (where.Contains("@src")) Add(cmd, "@src", source!);
                if (where.Contains("@dec")) Add(cmd, "@dec", decision!);

                await using var rd = await cmd.ExecuteReaderAsync(ct);
                while (await rd.ReadAsync(ct))
                {
                    string? S(int i) => rd.IsDBNull(i) ? null : rd.GetString(i);
                    d.Entries.Add(new RollEntryRow
                    {
                        Id = rd.GetInt32(0),
                        ExtractRowId = rd.IsDBNull(1) ? null : rd.GetInt32(1),
                        ValuationKey = S(2),
                        PremiseId = S(3),
                        PropertyType = rd.GetString(4),
                        Sector = S(5),
                        PropertyDescription = S(6),
                        Address = S(7),
                        Category = S(8),
                        MarketValue = rd.IsDBNull(9) ? null : rd.GetDecimal(9),
                        Source = rd.GetString(10),
                        RelatedToEntryId = rd.IsDBNull(11) ? null : rd.GetInt32(11),
                        Note = S(12),
                        Decision = rd.GetString(13),
                        DecidedBy = S(14),
                        DecidedAtUtc = rd.IsDBNull(15) ? null : rd.GetDateTime(15),
                        DecisionComment = S(16)
                    });
                }
            }

            // how many properties of the batch are still Pending (warn the Data Team)
            try
            {
                var table = await WeekTableAsync(d.Header.BatchId, ct);
                await using var cmd = await CommandAsync(ct);
                cmd.CommandText = $"SELECT COUNT(*) FROM {table} WHERE [Decision] = N'Pending';";
                d.UnverifiedInBatch = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
            }
            catch { d.UnverifiedInBatch = 0; }

            return d;
        }

        // ── Step 2 (manual until the LIS lookup exists) ───────────────────
        public async Task AddRelatedAsync(int id, RollRelatedInput input, string user, CancellationToken ct)
        {
            await RequireStatusAsync(id, "Draft", ct);

            var premise = (input.PremiseId ?? "").Trim();
            if (premise.Length == 0) throw new InvalidOperationException("Premise ID is required.");
            if (input.Source != "Review" && input.Source != "Amendment")
                throw new InvalidOperationException("Type must be Supplementary Review or Amendment.");

            await using var cmd = await CommandAsync(ct);
            // BR-02: a review / amendment belongs to the supplementary entry with the same Premise ID.
            cmd.CommandText = @"
DECLARE @parent INT = (SELECT TOP 1 Id FROM dbo.RollEntries
                       WHERE RollPreparationId = @id AND Source = N'Supplementary' AND PremiseId = @pid ORDER BY Id);
DECLARE @pt NVARCHAR(5) = ISNULL((SELECT TOP 1 PropertyType FROM dbo.RollEntries WHERE Id = @parent), N'FH');
DECLARE @sec NVARCHAR(100) = (SELECT TOP 1 Sector FROM dbo.RollEntries WHERE Id = @parent);
INSERT dbo.RollEntries
    (RollPreparationId, ValuationKey, PremiseId, PropertyType, Sector, PropertyDescription, MarketValue,
     Source, RelatedToEntryId, Note, AddedBy)
VALUES (@id, @vk, @pid, @pt, @sec, @desc, @mv, @src, @parent, @note, @u);
SELECT @parent;";
            Add(cmd, "@id", id);
            Add(cmd, "@pid", premise);
            Add(cmd, "@vk", (object?)Clean(input.ValuationKey) ?? DBNull.Value);
            Add(cmd, "@desc", (object?)Clean(input.PropertyDescription) ?? DBNull.Value);
            Add(cmd, "@mv", (object?)input.MarketValue ?? DBNull.Value);
            Add(cmd, "@src", input.Source);
            Add(cmd, "@note", (object?)Clean(input.Note) ?? DBNull.Value);
            Add(cmd, "@u", user);
            var parent = await cmd.ExecuteScalarAsync(ct);

            if (parent == null || parent is DBNull)
                throw new InvalidOperationException(
                    "Added, but no supplementary entry on this roll has that Premise ID, so it is not linked to one. Check the Premise ID.");
        }

        public async Task RemoveRelatedAsync(int id, int entryId, CancellationToken ct)
        {
            await RequireStatusAsync(id, "Draft", ct);
            await using var cmd = await CommandAsync(ct);
            cmd.CommandText = "DELETE dbo.RollEntries WHERE Id = @e AND RollPreparationId = @id AND ExtractRowId IS NULL AND Source <> N'Supplementary';";
            Add(cmd, "@e", entryId);
            Add(cmd, "@id", id);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // ── Step 3: submit for confirmation / Valuation Department decides ─
        public async Task SubmitAsync(int id, string user, CancellationToken ct)
        {
            await RequireStatusAsync(id, "Draft", ct);
            await using var cmd = await CommandAsync(ct);
            cmd.CommandText = @"
UPDATE dbo.RollPreparations
   SET Status = N'Submitted', SubmittedBy = @u, SubmittedAtUtc = SYSUTCDATETIME()
 WHERE Id = @id AND Status = N'Draft';";
            Add(cmd, "@u", user);
            Add(cmd, "@id", id);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task<int> DecideAsync(
            int id, IReadOnlyCollection<int> entryIds, string decision, string? comment, string user, CancellationToken ct)
        {
            await RequireStatusAsync(id, "Submitted", ct);
            if (decision != "Include" && decision != "Exclude" && decision != "Pending")
                throw new InvalidOperationException("Decision must be Include or Exclude.");
            if (entryIds == null || entryIds.Count == 0)
                throw new InvalidOperationException("Select at least one entry.");

            var ids = string.Join(",", entryIds.Select(i => i.ToString(CultureInfo.InvariantCulture)));
            await using var cmd = await CommandAsync(ct);
            cmd.CommandText = $@"
UPDATE dbo.RollEntries
   SET Decision = @d,
       DecidedBy = CASE WHEN @d = N'Pending' THEN NULL ELSE @u END,
       DecidedAtUtc = CASE WHEN @d = N'Pending' THEN NULL ELSE SYSUTCDATETIME() END,
       DecisionComment = @c
 WHERE RollPreparationId = @id AND Id IN ({ids});";
            Add(cmd, "@d", decision);
            Add(cmd, "@u", user);
            Add(cmd, "@c", (object?)Clean(comment) ?? DBNull.Value);
            Add(cmd, "@id", id);
            return await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task ConfirmAsync(int id, string user, CancellationToken ct)
        {
            await RequireStatusAsync(id, "Submitted", ct);

            await using (var chk = await CommandAsync(ct))
            {
                chk.CommandText = "SELECT COUNT(*) FROM dbo.RollEntries WHERE RollPreparationId = @id AND Decision = N'Pending';";
                Add(chk, "@id", id);
                var pending = Convert.ToInt32(await chk.ExecuteScalarAsync(ct));
                if (pending > 0)
                    throw new InvalidOperationException($"{pending} entr{(pending == 1 ? "y is" : "ies are")} still undecided. Include or exclude every entry before confirming.");
            }

            await using var cmd = await CommandAsync(ct);
            cmd.CommandText = @"
UPDATE dbo.RollPreparations
   SET Status = N'Confirmed', ConfirmedBy = @u, ConfirmedAtUtc = SYSUTCDATETIME()
 WHERE Id = @id AND Status = N'Submitted';";
            Add(cmd, "@u", user);
            Add(cmd, "@id", id);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // ── Confirmation List (BRD expected output) ───────────────────────
        public async Task<byte[]> ExportCsvAsync(int id, CancellationToken ct)
        {
            var d = await GetAsync(id, null, null, null, ct) ?? throw new InvalidOperationException("Roll not found.");

            string Q(string? v) => "\"" + (v ?? "").Replace("\"", "\"\"") + "\"";
            var sb = new StringBuilder();
            sb.AppendLine("Roll type," + Q(d.Header.RollType) + ",Roll number," + Q(d.Header.RollNumber) + ",GV," + Q(d.Header.GV) + ",Roll period," + Q(d.Header.RollPeriod));
            sb.AppendLine("Property type,Premise ID,Entry type,Valuation key,Sector,Property description,Address,Category,Market value,Decision,Decided by,Comment");
            foreach (var e in d.Entries)
                sb.AppendLine(string.Join(",",
                    e.PropertyType == "ST" ? "Sectional title" : "Freehold",
                    Q(e.PremiseId), e.Source, Q(e.ValuationKey), Q(e.Sector), Q(e.PropertyDescription), Q(e.Address), Q(e.Category),
                    e.MarketValue?.ToString("0.00", CultureInfo.InvariantCulture) ?? "",
                    e.Decision, Q(e.DecidedBy), Q(e.DecisionComment)));

            return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        }

        // ── helpers ───────────────────────────────────────────────────────
        private const string HeaderSql = @"
SELECT p.Id, p.BatchId, ISNULL(b.BatchName, N'') AS BatchName, p.RollType, p.RollNumber, p.GV, p.RollPeriod, p.Status,
       p.CreatedBy, p.CreatedAtUtc, p.SubmittedBy, p.SubmittedAtUtc, p.ConfirmedBy, p.ConfirmedAtUtc,
       (SELECT COUNT(*) FROM dbo.RollEntries e WHERE e.RollPreparationId = p.Id) AS Entries,
       (SELECT COUNT(*) FROM dbo.RollEntries e WHERE e.RollPreparationId = p.Id AND e.Decision = N'Pending') AS Pending,
       (SELECT COUNT(*) FROM dbo.RollEntries e WHERE e.RollPreparationId = p.Id AND e.Decision = N'Include') AS Included,
       (SELECT COUNT(*) FROM dbo.RollEntries e WHERE e.RollPreparationId = p.Id AND e.Decision = N'Exclude') AS Excluded
FROM dbo.RollPreparations p
LEFT JOIN dbo.Batches b ON b.Id = p.BatchId";

        private static RollPrepRow ReadHeader(DbDataReader rd) => new()
        {
            Id = rd.GetInt32(rd.GetOrdinal("Id")),
            BatchId = rd.GetInt32(rd.GetOrdinal("BatchId")),
            BatchName = rd.GetString(rd.GetOrdinal("BatchName")),
            RollType = rd.GetString(rd.GetOrdinal("RollType")),
            RollNumber = rd.GetString(rd.GetOrdinal("RollNumber")),
            GV = rd.GetString(rd.GetOrdinal("GV")),
            RollPeriod = rd.GetString(rd.GetOrdinal("RollPeriod")),
            Status = rd.GetString(rd.GetOrdinal("Status")),
            CreatedBy = rd.GetString(rd.GetOrdinal("CreatedBy")),
            CreatedAtUtc = rd.GetDateTime(rd.GetOrdinal("CreatedAtUtc")),
            Entries = rd.GetInt32(rd.GetOrdinal("Entries")),
            Pending = rd.GetInt32(rd.GetOrdinal("Pending")),
            Included = rd.GetInt32(rd.GetOrdinal("Included")),
            Excluded = rd.GetInt32(rd.GetOrdinal("Excluded"))
        };

        private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

        private async Task RequireStatusAsync(int id, string expected, CancellationToken ct)
        {
            await using var cmd = await CommandAsync(ct);
            cmd.CommandText = "SELECT Status FROM dbo.RollPreparations WHERE Id = @id;";
            Add(cmd, "@id", id);
            var s = await cmd.ExecuteScalarAsync(ct) as string;
            if (s == null) throw new InvalidOperationException("Roll not found.");
            if (!string.Equals(s, expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(expected == "Draft"
                    ? $"This roll is {s} and can no longer be changed."
                    : $"This roll is {s}. Decisions can only be made while it is Submitted.");
        }

        private async Task<string> WeekTableAsync(int batchId, CancellationToken ct)
        {
            var name = await _db.Batches.AsNoTracking()
                .Where(b => b.Id == batchId).Select(b => b.CleanWeekTableName).FirstOrDefaultAsync(ct);
            if (string.IsNullOrWhiteSpace(name) || !SafeName.IsMatch(name))
                throw new InvalidOperationException("This batch has no week table.");
            return $"[SECTION_78_NOTICE].[dbo].[{name}]";
        }

        private async Task<DbCommand> CommandAsync(CancellationToken ct)
        {
            var conn = _db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync(ct);
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
