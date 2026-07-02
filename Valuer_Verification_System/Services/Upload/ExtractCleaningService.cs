using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Models.Data;

namespace Valuer_Verification_System.Services.Upload
{
    public sealed class ExtractCleaningService : IExtractCleaningService
    {
        private readonly VvsDbContext _db;
        private readonly IConfiguration _config;

        public ExtractCleaningService(VvsDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        public async Task CleanSectorValuerAndResolveUsernameAsync(int batchId, string updatedBy, CancellationToken ct)
        {
            var rows = await _db.ExtractRows
                .Where(r => r.BatchId == batchId)
                .ToListAsync(ct);

            if (rows.Count == 0) return;

            var sectors = await LoadLatestSectorRowsAsync(ct);
            var users = await LoadUserManagementUsersAsync(ct);

            foreach (var row in rows)
            {
                row.TownNameDesc = CleanUpper(row.TownNameDesc);
                row.SgId = CleanUpper(row.SgId);

                var sectorMatch = FindSectorMatch(row, sectors);

                if (sectorMatch != null)
                {
                    row.Sector = Clean(sectorMatch.Sector);
                    row.Valuer = Clean(sectorMatch.Valuer);
                }

                ResolveUsername(row, users);

                row.UpdatedBy = updatedBy;
                row.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(ct);
        }

        // ─────────────────────────────────────────────────────────────────
        // Sector matching  (unchanged from original)
        // ─────────────────────────────────────────────────────────────────
        private static SectorRow? FindSectorMatch(ExtractRow row, List<SectorRow> sectors)
        {
            var town = CleanUpper(row.TownNameDesc);
            var sgId = CleanUpper(row.SgId);

            if (!string.IsNullOrWhiteSpace(town))
            {
                var byTown = sectors.FirstOrDefault(s =>
                    CleanUpper(s.TownNameDesc) == town);

                if (byTown != null) return byTown;
            }

            if (!string.IsNullOrWhiteSpace(sgId))
            {
                var sg8 = sgId.Length >= 8 ? sgId[..8] : sgId;
                var sg11 = sgId.Length >= 11 ? sgId[..11] : sgId;

                var byTc8 = sectors.FirstOrDefault(s =>
                    CleanUpper(s.Tc8) == sg8);
                if (byTc8 != null) return byTc8;

                var byTc11 = sectors.FirstOrDefault(s =>
                    CleanUpper(s.Tc11) == sg11);
                if (byTc11 != null) return byTc11;

                var byTc11First8 = sectors.FirstOrDefault(s =>
                {
                    var tc11 = CleanUpper(s.Tc11);
                    if (string.IsNullOrWhiteSpace(tc11)) return false;
                    var tc11First8 = tc11.Length >= 8 ? tc11[..8] : tc11;
                    return tc11First8 == sg8;
                });

                if (byTc11First8 != null) return byTc11First8;
            }

            return null;
        }

        // ─────────────────────────────────────────────────────────────────
        // Username resolution
        //
        // Sector VALUER column contains values like:
        //   "jSibeko(Jabulani Sibeko)"   — compact code + full name in brackets
        //   "jSibeko"                    — compact code only, no brackets
        //   "JOBURG\10112533"            — already a username
        //   "Jabulani Sibeko"            — full name only
        //
        // Match order (first hit wins):
        //   1. Exact full VALUER == Username or UsernameOld
        //   2. Text BEFORE bracket == Username or Username-without-domain or UsernameOld
        //   3. Text INSIDE bracket == FirstName Surname  (catches "jSibeko(Jabulani Sibeko)")
        //   4. Full VALUER == FirstName Surname  (catches "Jabulani Sibeko" with no brackets)
        //   5. Text BEFORE bracket == FirstInitial+Surname  (catches "jSibeko" with no brackets)
        //   6. Full VALUER == FirstInitial+Surname  (compact code with no brackets at all)
        //   7. Inside bracket == FirstInitial+Surname  (compact inside brackets, rare)
        // ─────────────────────────────────────────────────────────────────
        private static void ResolveUsername(ExtractRow row, List<UserRow> users)
        {
            row.Username = null;
            row.ResolvedUserId = null;
            row.ResolvedValuerEmail = null;
            row.ValuerMatchStatus = null;
            row.ValuerMatchComment = null;

            var valuer = Clean(row.Valuer);

            if (string.IsNullOrWhiteSpace(valuer))
            {
                row.ValuerMatchStatus = "EmptyValuer";
                row.ValuerMatchComment = "Valuer field is empty after sector cleaning.";
                return;
            }

            // Split the valuer value into its two parts once
            var outsideBracket = valuer.Contains('(')
                ? valuer[..valuer.IndexOf('(')].Trim()
                : valuer;

            var insideBracket = ExtractInsideBracket(valuer);

            UserRow? match;

            // ── Step 1: exact full VALUER == Username or UsernameOld ──────────
            match = users.FirstOrDefault(u =>
                Same(valuer, u.Username) ||
                Same(valuer, u.UsernameOld));

            if (match != null)
            {
                ApplyUser(row, match, "Matched by exact Username.");
                return;
            }

            // ── Step 2: outside bracket == Username / UsernameOld / no-domain ─
            if (!string.IsNullOrWhiteSpace(outsideBracket))
            {
                match = users.FirstOrDefault(u =>
                    Same(outsideBracket, u.Username) ||
                    Same(outsideBracket, u.UsernameOld) ||
                    Same(outsideBracket, u.UsernameNoDomain));

                if (match != null)
                {
                    ApplyUser(row, match, "Matched by username before brackets.");
                    return;
                }
            }

            // ── Step 3: inside bracket == FirstName Surname ───────────────────
            // Catches "jSibeko(Jabulani Sibeko)" → inside = "Jabulani Sibeko"
            if (!string.IsNullOrWhiteSpace(insideBracket))
            {
                match = users.FirstOrDefault(u =>
                    Same(insideBracket, u.FullName1) ||
                    Same(insideBracket, u.FullName2) ||
                    Same(insideBracket, u.FullName3));

                if (match != null)
                {
                    ApplyUser(row, match, "Matched by full name inside brackets.");
                    return;
                }
            }

            // ── Step 4: full VALUER == FirstName Surname ──────────────────────
            // Catches "Jabulani Sibeko" (no brackets)
            match = users.FirstOrDefault(u =>
                Same(valuer, u.FullName1) ||
                Same(valuer, u.FullName2) ||
                Same(valuer, u.FullName3));

            if (match != null)
            {
                ApplyUser(row, match, "Matched by direct full name.");
                return;
            }

            // ── Step 5: outside bracket == FirstInitial+Surname ───────────────
            // Catches "jSibeko(Jabulani Sibeko)" → outside = "jSibeko" → "JSIBEKO"
            // and also "jSibeko" with no brackets at all
            if (!string.IsNullOrWhiteSpace(outsideBracket))
            {
                match = users.FirstOrDefault(u =>
                    Same(outsideBracket, u.CompactCode));

                if (match != null)
                {
                    ApplyUser(row, match, "Matched by compact code (FirstInitial+Surname) before brackets.");
                    return;
                }
            }

            // ── Step 6: full VALUER == FirstInitial+Surname (no brackets at all)
            match = users.FirstOrDefault(u =>
                Same(valuer, u.CompactCode));

            if (match != null)
            {
                ApplyUser(row, match, "Matched by compact code (FirstInitial+Surname) direct.");
                return;
            }

            // ── Step 7: inside bracket == FirstInitial+Surname (rare edge case)
            if (!string.IsNullOrWhiteSpace(insideBracket))
            {
                match = users.FirstOrDefault(u =>
                    Same(insideBracket, u.CompactCode));

                if (match != null)
                {
                    ApplyUser(row, match, "Matched by compact code inside brackets.");
                    return;
                }
            }

            // ── No match ─────────────────────────────────────────────────────
            row.ValuerMatchStatus = "NotFound";
            row.ValuerMatchComment = $"No active UserManagement user found for Valuer: {valuer}";
        }

        private static void ApplyUser(ExtractRow row, UserRow user, string comment)
        {
            row.ResolvedUserId = user.UserId;
            row.Username = Clean(user.Username);
            row.ResolvedValuerEmail = Clean(user.EmailAddress);
            row.ValuerMatchStatus = "Matched";
            row.ValuerMatchComment = comment;
        }

        // ─────────────────────────────────────────────────────────────────
        // Data loaders
        // ─────────────────────────────────────────────────────────────────
        private async Task<List<SectorRow>> LoadLatestSectorRowsAsync(CancellationToken ct)
        {
            var connString = _db.Database.GetConnectionString();

            await using var conn = new SqlConnection(connString);
            await conn.OpenAsync(ct);

            string? tableName;

            await using (var cmd = new SqlCommand(
                """
                SELECT TOP (1) name
                FROM sys.tables
                WHERE name LIKE 'SECTORS[_]%'
                ORDER BY name DESC
                """, conn))
            {
                tableName = (string?)await cmd.ExecuteScalarAsync(ct);
            }

            if (string.IsNullOrWhiteSpace(tableName))
                throw new InvalidOperationException("No SECTORS table found in Valuer_Verification_DB.");

            var sql =
                $"""
                SELECT
                    TOWN_NAME_DESC,
                    TC_8,
                    TC_11,
                    SECTOR,
                    VALUER
                FROM dbo.[{tableName}]
                """;

            var result = new List<SectorRow>();

            await using var query = new SqlCommand(sql, conn);
            await using var reader = await query.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                result.Add(new SectorRow
                {
                    TownNameDesc = ReadString(reader, "TOWN_NAME_DESC"),
                    Tc8 = ReadString(reader, "TC_8"),
                    Tc11 = ReadString(reader, "TC_11"),
                    Sector = ReadString(reader, "SECTOR"),
                    Valuer = ReadString(reader, "VALUER")
                });
            }

            return result;
        }

        private async Task<List<UserRow>> LoadUserManagementUsersAsync(CancellationToken ct)
        {
            var connString = _config.GetConnectionString("UserManagement")
                ?? throw new InvalidOperationException("Connection string 'UserManagement' is missing.");

            await using var conn = new SqlConnection(connString);
            await conn.OpenAsync(ct);

            const string sql =
                """
                SELECT
                    UserID,
                    Username,
                    UsernameOld,
                    FirstName,
                    SecondName,
                    Surname,
                    EmailAddress,
                    Active
                FROM dbo.Users
                WHERE ISNULL(Active, 1) = 1
                """;

            var result = new List<UserRow>();

            await using var cmd = new SqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                var first = ReadString(reader, "FirstName")?.Trim();
                var second = ReadString(reader, "SecondName")?.Trim();
                var surname = ReadString(reader, "Surname")?.Trim();
                var username = ReadString(reader, "Username")?.Trim();
                var usernameOld = ReadString(reader, "UsernameOld")?.Trim();

                // FirstName without double-barrelled prefix (e.g. "Mary Jane" → "Mary")
                var firstShort = first != null && first.Contains(' ')
                    ? first[..first.IndexOf(' ')].Trim()
                    : first;

                // Username without domain prefix (e.g. "JOBURG\10112533" → "10112533")
                var usernameNoDomain = username != null && username.Contains('\\')
                    ? username[(username.IndexOf('\\') + 1)..].Trim()
                    : username;

                result.Add(new UserRow
                {
                    UserId = ReadInt(reader, "UserID"),
                    Username = username,
                    UsernameOld = usernameOld,
                    UsernameNoDomain = usernameNoDomain,
                    FirstName = first,
                    SecondName = second,
                    Surname = surname,
                    EmailAddress = ReadString(reader, "EmailAddress"),

                    // "JABULANI SIBEKO"
                    FullName1 = BuildFullName(first, null, surname),

                    // "JABULANI SIBEKO" or "JABULANI JANE SIBEKO" when second name exists
                    FullName2 = BuildFullName(first, second, surname),

                    // "MARY SIBEKO" when first = "Mary Jane" (short first + surname)
                    FullName3 = BuildFullName(firstShort, null, surname),

                    // "JSIBEKO" — first initial + surname, matches compact codes like "jSibeko"
                    CompactCode = BuildCompactCode(first, surname)
                });
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────
        private static string? BuildFullName(string? first, string? second, string? surname)
        {
            var parts = new[] { first, second, surname }
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p!.Trim());

            var name = string.Join(" ", parts).Trim();
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }

        private static string? BuildCompactCode(string? first, string? surname)
        {
            if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(surname))
                return null;

            // jSibeko → "J" + "SIBEKO" = "JSIBEKO"
            return (first.Trim()[0].ToString() + surname.Trim()).ToUpperInvariant();
        }

        private static string? ReadString(SqlDataReader r, string col)
        {
            var idx = r.GetOrdinal(col);
            return r.IsDBNull(idx) ? null : r.GetValue(idx)?.ToString();
        }

        private static int? ReadInt(SqlDataReader r, string col)
        {
            var idx = r.GetOrdinal(col);
            if (r.IsDBNull(idx)) return null;
            return Convert.ToInt32(r.GetValue(idx));
        }

        private static string? Clean(string? s)
            => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private static string? CleanUpper(string? s)
            => string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToUpperInvariant();

        private static bool Same(string? a, string? b)
            => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

        private static string? ExtractInsideBracket(string value)
        {
            var start = value.IndexOf('(');
            var end = value.IndexOf(')');
            if (start < 0 || end <= start) return null;
            return value.Substring(start + 1, end - start - 1).Trim();
        }

        // ─────────────────────────────────────────────────────────────────
        // Private record types
        // ─────────────────────────────────────────────────────────────────
        private sealed class SectorRow
        {
            public string? TownNameDesc { get; set; }
            public string? Tc8 { get; set; }
            public string? Tc11 { get; set; }
            public string? Sector { get; set; }
            public string? Valuer { get; set; }
        }

        private sealed class UserRow
        {
            public int? UserId { get; set; }
            public string? Username { get; set; }
            public string? UsernameOld { get; set; }
            public string? UsernameNoDomain { get; set; }
            public string? FirstName { get; set; }
            public string? SecondName { get; set; }
            public string? Surname { get; set; }
            public string? EmailAddress { get; set; }
            public string? FullName1 { get; set; }   // FirstName + Surname
            public string? FullName2 { get; set; }   // FirstName + SecondName + Surname
            public string? FullName3 { get; set; }   // ShortFirst + Surname
            public string? CompactCode { get; set; }   // FirstInitial + Surname (e.g. JSIBEKO)
        }
    }
}