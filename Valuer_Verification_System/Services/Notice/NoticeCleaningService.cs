using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Models.ViewModels;

namespace Valuer_Verification_System.Services.Notice
{
    /// <summary>
    /// Executes each cleaning stored procedure against ExtractRows
    /// (VVS database on 118 server), then creates the clean week table
    /// on the 173 server (GV2018_SECTION_78).
    ///
    /// Each step calls sp_VVS_Clean_&lt;StepName&gt; with @ExtractionDate,
    /// @LisMasterTable, @SapTable parameters.
    /// The stored procedures are SQL scripts in /SQL/StoredProcedures/.
    /// </summary>
    public sealed class NoticeCleaningService : INoticeCleaningService
    {
        private readonly VvsDbContext _db;
        private readonly IConfiguration _config;

        // Execution order matches the dependency chain in the SQL scripts
        private static readonly string[] _steps = new[]
        {
            "UnitType",
            "PremiseId",
            "ErfPtnRe",
            "SchemeInfo",
            "PropertyDescription",
            "SapAddresses",
            "LisStreetAddress",
            "UsableFlags",
            "CojOwned",
            "FallbackAddresses",
            "FinalMappings",
            "Trim"
        };

        public IReadOnlyList<string> StepNames => _steps;

        public NoticeCleaningService(VvsDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        // ── Table name builder ────────────────────────────────────────────────

        public string BuildWeekTableName(DateOnly extractionDate)
        {
            // Determine which week of the month (W1–W5)
            int weekNo = GetWeekOfMonth(extractionDate);
            return $"GV23_LIS_SV_{extractionDate.Year}_{extractionDate.Month:D2}_EXTRACT_W{weekNo}";
        }

        private static int GetWeekOfMonth(DateOnly date)
        {
            // Week 1 = days 1–7, Week 2 = 8–14, etc.
            return (int)Math.Ceiling(date.Day / 7.0);
        }

        // ── Step runner ───────────────────────────────────────────────────────

        public async Task<CleaningStepResultVm> RunStepAsync(
            string stepName,
            DateOnly extractionDate,
            string lisMasterTable,
            string sapTable,
            CancellationToken ct)
        {
            var spName = $"sp_VVS_Clean_{stepName}";

            try
            {
                var conn = _db.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open)
                    await conn.OpenAsync(ct);

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = spName;
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.CommandTimeout = 300; // 5 min per step

                cmd.Parameters.Add(new SqlParameter("@ExtractionDate", extractionDate.ToDateTime(TimeOnly.MinValue)));
                cmd.Parameters.Add(new SqlParameter("@LisMasterTable", lisMasterTable));
                cmd.Parameters.Add(new SqlParameter("@SapTable", sapTable));

                // Capture rows affected via OUTPUT param
                var rowsParam = new SqlParameter("@RowsAffected", System.Data.SqlDbType.Int)
                {
                    Direction = System.Data.ParameterDirection.Output
                };
                cmd.Parameters.Add(rowsParam);

                await cmd.ExecuteNonQueryAsync(ct);

                int rows = rowsParam.Value != DBNull.Value ? (int)rowsParam.Value : 0;

                return new CleaningStepResultVm
                {
                    Success = true,
                    StepName = stepName,
                    RowsAffected = rows
                };
            }
            catch (Exception ex)
            {
                return new CleaningStepResultVm
                {
                    Success = false,
                    StepName = stepName,
                    Error = ex.Message
                };
            }
        }

        // ── Create week table on 173 server ───────────────────────────────────

        public async Task<CleaningStepResultVm> CreateWeekTableAsync(
            string weekTableName,
            DateOnly extractionDate,
            CancellationToken ct)
        {
            var cs173 = _config.GetConnectionString("GV2018Section78");
            if (string.IsNullOrWhiteSpace(cs173))
                return new CleaningStepResultVm
                {
                    Success = false,
                    StepName = "CreateWeekTable",
                    Error = "Connection string 'GV2018Section78' not found in appsettings."
                };

            try
            {
                await using var conn = new SqlConnection(cs173);
                await conn.OpenAsync(ct);

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "sp_VVS_CreateWeekTable";
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.CommandTimeout = 600; // 10 min – may copy many rows

                cmd.Parameters.Add(new SqlParameter("@TableName", weekTableName));
                cmd.Parameters.Add(new SqlParameter("@ExtractionDate", extractionDate.ToDateTime(TimeOnly.MinValue)));
                cmd.Parameters.Add(new SqlParameter("@SourceServer", _config["GV2018:SourceServer"] ?? "168.89.27.118"));
                cmd.Parameters.Add(new SqlParameter("@SourceDatabase", _config["GV2018:SourceDatabase"] ?? "Valuer_Verification_DB"));

                var rowsParam = new SqlParameter("@RowsAffected", System.Data.SqlDbType.Int)
                {
                    Direction = System.Data.ParameterDirection.Output
                };
                cmd.Parameters.Add(rowsParam);

                await cmd.ExecuteNonQueryAsync(ct);

                int rows = rowsParam.Value != DBNull.Value ? (int)rowsParam.Value : 0;

                return new CleaningStepResultVm
                {
                    Success = true,
                    StepName = "CreateWeekTable",
                    RowsAffected = rows
                };
            }
            catch (Exception ex)
            {
                return new CleaningStepResultVm
                {
                    Success = false,
                    StepName = "CreateWeekTable",
                    Error = ex.Message
                };
            }
        }
    }
}