using Microsoft.Data.SqlClient;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Valuer_Verification_System.Services.Notice
{
    public interface ILatestCleaningTableResolver
    {
        Task<LatestCleaningTables> ResolveAsync(CancellationToken ct);
    }

    public sealed class LatestCleaningTables
    {
        public string LisTable { get; set; } = "";
        public string SapTable { get; set; } = "";
    }

    public sealed class LatestCleaningTableResolver : ILatestCleaningTableResolver
    {
        private readonly string _connectionString;
        private readonly ILogger<LatestCleaningTableResolver> _logger;

        private static readonly Regex LisRegex = new(
            @"^LIS_(\d{8})$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex SapRegex = new(
            @"^SAP_Statements_(\d{4})_(\d{2})$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public LatestCleaningTableResolver(
            IConfiguration config,
            ILogger<LatestCleaningTableResolver> logger)
        {
            _connectionString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

            _logger = logger;
        }

        public async Task<LatestCleaningTables> ResolveAsync(CancellationToken ct)
        {
            var tableNames = await GetUserTablesAsync(ct);

            var latestLis = tableNames
                .Select(x => new
                {
                    TableName = x,
                    Match = LisRegex.Match(x)
                })
                .Where(x => x.Match.Success)
                .Select(x => new
                {
                    x.TableName,
                    Date = DateOnly.ParseExact(
                        x.Match.Groups[1].Value,
                        "yyyyMMdd",
                        CultureInfo.InvariantCulture)
                })
                .OrderByDescending(x => x.Date)
                .FirstOrDefault();

            var latestSap = tableNames
                .Select(x => new
                {
                    TableName = x,
                    Match = SapRegex.Match(x)
                })
                .Where(x => x.Match.Success)
                .Select(x => new
                {
                    x.TableName,
                    Date = new DateOnly(
                        int.Parse(x.Match.Groups[1].Value),
                        int.Parse(x.Match.Groups[2].Value),
                        1)
                })
                .OrderByDescending(x => x.Date)
                .FirstOrDefault();

            if (latestLis is null)
                throw new InvalidOperationException("No LIS table found. Expected table name like LIS_20260116.");

            if (latestSap is null)
                throw new InvalidOperationException("No SAP statement table found. Expected table name like SAP_Statements_2026_01.");

            var result = new LatestCleaningTables
            {
                LisTable = latestLis.TableName,
                SapTable = latestSap.TableName
            };

            _logger.LogInformation(
                "Resolved latest cleaning tables. LIS: {LisTable}, SAP: {SapTable}.",
                result.LisTable,
                result.SapTable);

            return result;
        }

        private async Task<List<string>> GetUserTablesAsync(CancellationToken ct)
        {
            var tables = new List<string>();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            await using var cmd = new SqlCommand(@"
SELECT TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE = 'BASE TABLE'
AND TABLE_SCHEMA = 'dbo'
AND
(
    TABLE_NAME LIKE 'LIS[_]20%'
    OR TABLE_NAME LIKE 'SAP[_]Statements[_]20%'
)
ORDER BY TABLE_NAME;", conn);

            await using var reader = await cmd.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                tables.Add(reader.GetString(0));
            }

            return tables;
        }
    }
}