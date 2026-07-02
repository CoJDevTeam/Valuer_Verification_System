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

            UserRow? match = null;

            // 1. Exact username
            match = users.FirstOrDefault(u =>
                Same(valuer, u.Username));

            if (match != null)
            {
                ApplyUser(row, match, "Matched Valuer to UserManagement by exact Username.");
                return;
            }

            // 2. Username before bracket
            var outsideBracket = valuer.Contains("(")
                ? valuer[..valuer.IndexOf("(")].Trim()
                : valuer;

            match = users.FirstOrDefault(u =>
                Same(outsideBracket, u.Username));

            if (match != null)
            {
                ApplyUser(row, match, "Matched Valuer to UserManagement by username before brackets.");
                return;
            }

            // 3. Full name inside bracket
            var insideBracket = ExtractInsideBracket(valuer);

            if (!string.IsNullOrWhiteSpace(insideBracket))
            {
                match = users.FirstOrDefault(u =>
                    Same(insideBracket, u.FullName1) ||
                    Same(insideBracket, u.FullName2));

                if (match != null)
                {
                    ApplyUser(row, match, "Matched Valuer to UserManagement by full name inside brackets.");
                    return;
                }
            }

            // 4. Direct full name
            match = users.FirstOrDefault(u =>
                Same(valuer, u.FullName1) ||
                Same(valuer, u.FullName2));

            if (match != null)
            {
                ApplyUser(row, match, "Matched Valuer to UserManagement by FirstName + Surname.");
                return;
            }

            row.ValuerMatchStatus = "NotFound";
            row.ValuerMatchComment = "No active UserManagement user found for this Valuer.";
        }

        private static void ApplyUser(ExtractRow row, UserRow user, string comment)
        {
            row.ResolvedUserId = user.UserId;
            row.Username = Clean(user.Username);
            row.ResolvedValuerEmail = Clean(user.EmailAddress);
            row.ValuerMatchStatus = "Matched";
            row.ValuerMatchComment = comment;
        }

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

            var sql =
                """
                SELECT 
                    UserID,
                    Username,
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
                var first = ReadString(reader, "FirstName");
                var second = ReadString(reader, "SecondName");
                var surname = ReadString(reader, "Surname");

                result.Add(new UserRow
                {
                    UserId = ReadInt(reader, "UserID"),
                    Username = ReadString(reader, "Username"),
                    FirstName = first,
                    SecondName = second,
                    Surname = surname,
                    EmailAddress = ReadString(reader, "EmailAddress"),
                    FullName1 = Clean($"{first} {surname}"),
                    FullName2 = Clean($"{first} {second} {surname}")
                });
            }

            return result;
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
            => string.Equals(Clean(a), Clean(b), StringComparison.OrdinalIgnoreCase);

        private static string? ExtractInsideBracket(string value)
        {
            var start = value.IndexOf("(");
            var end = value.IndexOf(")");

            if (start < 0 || end <= start) return null;

            return value.Substring(start + 1, end - start - 1).Trim();
        }

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
            public string? FirstName { get; set; }
            public string? SecondName { get; set; }
            public string? Surname { get; set; }
            public string? EmailAddress { get; set; }
            public string? FullName1 { get; set; }
            public string? FullName2 { get; set; }
        }
    }
}