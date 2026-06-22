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

        public async Task<ValuerUsernameResolution> ResolveAsync(string? valuerName, CancellationToken ct)
        {
            valuerName = Normalize(valuerName);

            if (string.IsNullOrWhiteSpace(valuerName))
            {
                return new ValuerUsernameResolution
                {
                    Status = "EmptyValuer",
                    Comment = "The extract VALUER column is empty."
                };
            }

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            var matches = await FindMatchesAsync(conn, valuerName, ct);

            if (matches.Count == 1)
            {
                var m = matches[0];

                return new ValuerUsernameResolution
                {
                    UserId = m.UserId,
                    Username = m.Username,
                    EmailAddress = m.EmailAddress,
                    Status = "Matched",
                    Comment = $"Matched extract valuer '{valuerName}' to UserManagement username '{m.Username}'."
                };
            }

            if (matches.Count > 1)
            {
                return new ValuerUsernameResolution
                {
                    Status = "MultipleMatches",
                    Comment = $"Multiple active users matched extract valuer '{valuerName}'. Please fix UserManagement or extract valuer name."
                };
            }

            return new ValuerUsernameResolution
            {
                Status = "NotFound",
                Comment = $"No active UserManagement user found for extract valuer '{valuerName}'."
            };
        }

        private static async Task<List<UserMatch>> FindMatchesAsync(
            SqlConnection conn,
            string valuerName,
            CancellationToken ct)
        {
            var results = new List<UserMatch>();

            await using var cmd = new SqlCommand(@"
SELECT TOP (20)
       [UserID],
       [FirstName],
       [SecondName],
       [Surname],
       [EmailAddress],
       [Username],
       [UsernameOld],
       [SAPNumber],
       [SAPNumberOld],
       [Sector],
       [Position],
       [Active]
FROM [dbo].[Users]
WHERE ISNULL([Active], 0) = 1
AND
(
    UPPER(LTRIM(RTRIM(ISNULL([Username], '')))) = UPPER(@Valuer)
    OR UPPER(LTRIM(RTRIM(ISNULL([UsernameOld], '')))) = UPPER(@Valuer)
    OR UPPER(LTRIM(RTRIM(ISNULL([SAPNumber], '')))) = UPPER(@Valuer)
    OR UPPER(LTRIM(RTRIM(ISNULL([SAPNumberOld], '')))) = UPPER(@Valuer)
    OR UPPER(LTRIM(RTRIM(ISNULL([FirstName], '') + ' ' + ISNULL([Surname], '')))) LIKE '%' + UPPER(@Valuer) + '%'
    OR UPPER(LTRIM(RTRIM(ISNULL([Surname], '') + ' ' + ISNULL([FirstName], '')))) LIKE '%' + UPPER(@Valuer) + '%'
    OR UPPER(LTRIM(RTRIM(ISNULL([FirstName], '') + ' ' + ISNULL([SecondName], '') + ' ' + ISNULL([Surname], '')))) LIKE '%' + UPPER(@Valuer) + '%'
)
ORDER BY [UserID];", conn);

            cmd.CommandType = CommandType.Text;
            cmd.Parameters.AddWithValue("@Valuer", valuerName);

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