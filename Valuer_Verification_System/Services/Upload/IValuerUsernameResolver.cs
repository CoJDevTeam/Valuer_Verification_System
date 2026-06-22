using Microsoft.Data.SqlClient;
using System.Data;

namespace Valuer_Verification_System.Services.Upload
{
    public interface IValuerUsernameResolver
    {
        Task<ValuerUsernameResolution> ResolveAsync(string? valuerName, CancellationToken ct);
    }

    public sealed class ValuerUsernameResolution
    {
        public int? UserId { get; set; }
        public string? Username { get; set; }
        public string? EmailAddress { get; set; }
        public string Status { get; set; } = "NotFound";
        public string? Comment { get; set; }
    }

    public sealed class ValuerUsernameResolver : IValuerUsernameResolver
    {
        private readonly string _connectionString;

        public ValuerUsernameResolver(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("UserManagement")
                ?? throw new InvalidOperationException("Connection string 'UserManagement' is missing.");
        }


        private sealed class ValuerNameParts
        {
            public string Raw { get; set; } = "";
            public string OutsideBrackets { get; set; } = "";
            public string InsideBrackets { get; set; } = "";
            public string FirstName { get; set; } = "";
            public string Surname { get; set; } = "";
        }

        private static ValuerNameParts ParseValuerName(string? value)
        {
            var raw = (value ?? "").Trim();

            var result = new ValuerNameParts
            {
                Raw = raw,
                OutsideBrackets = raw
            };

            if (string.IsNullOrWhiteSpace(raw))
                return result;

            var open = raw.IndexOf('(');
            var close = raw.LastIndexOf(')');

            if (open >= 0 && close > open)
            {
                result.OutsideBrackets = raw[..open].Trim();
                result.InsideBrackets = raw.Substring(open + 1, close - open - 1).Trim();
            }

            var nameToSplit = !string.IsNullOrWhiteSpace(result.InsideBrackets)
                ? result.InsideBrackets
                : raw;

            var parts = nameToSplit
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            if (parts.Count >= 2)
            {
                result.FirstName = parts.First();
                result.Surname = parts.Last();
            }
            else if (parts.Count == 1)
            {
                result.FirstName = parts[0];
            }

            return result;
        }
        public async Task<ValuerUsernameResolution> ResolveAsync(string? valuerName, CancellationToken ct)
        {
            var originalValuerName = valuerName;

            valuerName = Normalize(valuerName);

            if (string.IsNullOrWhiteSpace(valuerName))
            {
                return new ValuerUsernameResolution
                {
                    Status = "EmptyValuer",
                    Comment = "The extract VALUER column is empty."
                };
            }

            var parts = ParseValuerName(valuerName);

            if (string.IsNullOrWhiteSpace(parts.Raw))
            {
                return new ValuerUsernameResolution
                {
                    Status = "EmptyValuer",
                    Comment = "Valuer field is empty."
                };
            }

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            var matches = await FindMatchesAsync(conn, parts, ct);

            if (matches.Count == 1)
            {
                var m = matches[0];

                return new ValuerUsernameResolution
                {
                    UserId = m.UserId,
                    Username = m.Username,
                    EmailAddress = m.EmailAddress,
                    Status = "Matched",
                    Comment = string.IsNullOrWhiteSpace(parts.InsideBrackets)
                        ? $"Matched extract valuer '{parts.Raw}' to UserManagement username '{m.Username}'."
                        : $"Matched extract valuer '{parts.Raw}' using username '{parts.OutsideBrackets}' and name '{parts.InsideBrackets}' to UserManagement username '{m.Username}'."
                };
            }

            if (matches.Count > 1)
            {
                var matchedUsers = string.Join(", ", matches.Select(x => x.Username));

                return new ValuerUsernameResolution
                {
                    Status = "MultipleMatches",
                    Comment = string.IsNullOrWhiteSpace(parts.InsideBrackets)
                        ? $"Multiple active users matched extract valuer '{parts.Raw}'. Matches: {matchedUsers}. Please fix UserManagement or extract valuer name."
                        : $"Multiple active users matched extract valuer '{parts.Raw}' using username '{parts.OutsideBrackets}' and name '{parts.InsideBrackets}'. Matches: {matchedUsers}. Please fix UserManagement or extract valuer name."
                };
            }

            return new ValuerUsernameResolution
            {
                Status = "NotFound",
                Comment = string.IsNullOrWhiteSpace(parts.InsideBrackets)
                    ? $"No active UserManagement user found for extract valuer '{parts.Raw}'."
                    : $"No active UserManagement user found for extract valuer '{parts.Raw}' using username '{parts.OutsideBrackets}' and name '{parts.InsideBrackets}'."
            };
        }

        private static async Task<List<UserMatch>> FindMatchesAsync(
            SqlConnection conn,
            ValuerNameParts parts,
            CancellationToken ct)
        {
            var results = new List<UserMatch>();

            await using var cmd = new SqlCommand(@"
SELECT TOP (20)
    UserID,
    FirstName,
    SecondName,
    Surname,
    EmailAddress,
    Username,
    UsernameOld,
    SAPNumber,
    SAPNumberOld,
    Sector,
    Position,
    Active
FROM [UserManagement].[dbo].[Users]
WHERE ISNULL(Active, 1) = 1
AND
(
       LTRIM(RTRIM(ISNULL(Username, ''))) = @Raw
    OR LTRIM(RTRIM(ISNULL(UsernameOld, ''))) = @Raw
    OR LTRIM(RTRIM(ISNULL(SAPNumber, ''))) = @Raw
    OR LTRIM(RTRIM(ISNULL(SAPNumberOld, ''))) = @Raw

    OR LTRIM(RTRIM(ISNULL(Username, ''))) = @Outside
    OR LTRIM(RTRIM(ISNULL(UsernameOld, ''))) = @Outside
    OR LTRIM(RTRIM(ISNULL(SAPNumber, ''))) = @Outside
    OR LTRIM(RTRIM(ISNULL(SAPNumberOld, ''))) = @Outside

    OR (
           @FirstName <> ''
       AND @Surname <> ''
       AND FirstName LIKE '%' + @FirstName + '%'
       AND Surname LIKE '%' + @Surname + '%'
    )

    OR (
           @Inside <> ''
       AND LTRIM(RTRIM(
            ISNULL(FirstName, '') + ' ' +
            ISNULL(SecondName, '') + ' ' +
            ISNULL(Surname, '')
       )) LIKE '%' + @Inside + '%'
    )

    OR (
           @Inside <> ''
       AND LTRIM(RTRIM(
            ISNULL(FirstName, '') + ' ' +
            ISNULL(Surname, '')
       )) LIKE '%' + @Inside + '%'
    )
);", conn);

            cmd.CommandType = CommandType.Text;

            cmd.Parameters.AddWithValue("@Raw", parts.Raw);
            cmd.Parameters.AddWithValue("@Outside", parts.OutsideBrackets);
            cmd.Parameters.AddWithValue("@Inside", parts.InsideBrackets);
            cmd.Parameters.AddWithValue("@FirstName", parts.FirstName);
            cmd.Parameters.AddWithValue("@Surname", parts.Surname);

            await using var reader = await cmd.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                results.Add(new UserMatch
                {
                    UserId = reader["UserID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["UserID"]),
                    Username = reader["Username"]?.ToString()?.Trim() ?? "",
                    EmailAddress = reader["EmailAddress"]?.ToString()?.Trim()
                });
            }

            return results
                .Where(x => !string.IsNullOrWhiteSpace(x.Username))
                .GroupBy(x => x.Username, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
        }
        private static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            return value.Trim()
                .Replace("  ", " ")
                .Replace("\t", " ");
        }

        private sealed class UserMatch
        {
            public int UserId { get; set; }
            public string Username { get; set; } = "";
            public string? EmailAddress { get; set; }
        }
    }
}