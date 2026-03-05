using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Models.ViewModels;

namespace Valuer_Verification_System.Services.Notice
{
    /// <summary>
    /// Executes each cleaning stored procedure against ExtractRows on 118 server
    /// (Valuer_Verification_DB), then creates the clean week table on the same
    /// 118 server in the SECTION_78_NOTICE database.
    ///
    /// LIS and SAP tables are also on 118 inside Valuer_Verification_DB —
    /// the SPs reference them directly without a linked-server prefix.
    /// </summary>
    public sealed class NoticeCleaningService : INoticeCleaningService
    {
        private readonly VvsDbContext _db;
        private readonly IConfiguration _config;

        private static readonly string[] _steps =
        {
            "UnitType",
            "PremiseId",
            "ErfPtnRe",
            "Dates",              // Stamp LetterDate, ReviewOpenDate, ReviewCloseDate from WeekWindow
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
            int weekNo = (int)Math.Ceiling(extractionDate.Day / 7.0);
            return $"GV23_LIS_SV_{extractionDate.Year}_{extractionDate.Month:D2}_EXTRACT_W{weekNo}";
        }

        // ── Cleaning step runner (runs on Valuer_Verification_DB, 118) ────────

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
                cmd.CommandTimeout = 300;

                cmd.Parameters.Add(new SqlParameter("@ExtractionDate", extractionDate.ToDateTime(TimeOnly.MinValue)));
                cmd.Parameters.Add(new SqlParameter("@LisMasterTable", lisMasterTable));
                cmd.Parameters.Add(new SqlParameter("@SapTable", sapTable));

                var rowsParam = new SqlParameter("@RowsAffected", System.Data.SqlDbType.Int)
                { Direction = System.Data.ParameterDirection.Output };
                cmd.Parameters.Add(rowsParam);

                await cmd.ExecuteNonQueryAsync(ct);

                int rows = rowsParam.Value != DBNull.Value ? (int)rowsParam.Value : 0;

                return new CleaningStepResultVm { Success = true, StepName = stepName, RowsAffected = rows };
            }
            catch (Exception ex)
            {
                return new CleaningStepResultVm { Success = false, StepName = stepName, Error = ex.Message };
            }
        }

        // ── Create week table on 118 server · SECTION_78_NOTICE ──────────────

        public async Task<CleaningStepResultVm> CreateWeekTableAsync(
            string weekTableName,
            DateOnly extractionDate,
            CancellationToken ct)
        {
            var cs = _config.GetConnectionString("Section78Notice");
            if (string.IsNullOrWhiteSpace(cs))
                return new CleaningStepResultVm
                {
                    Success = false,
                    StepName = "CreateWeekTable",
                    Error = "Connection string 'Section78Notice' not found in appsettings."
                };

            try
            {
                await using var conn = new SqlConnection(cs);
                await conn.OpenAsync(ct);

                await using var cmd = conn.CreateCommand();
                
                cmd.CommandText = "sp_VVS_CreateWeekTable";
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.CommandTimeout = 600;

                cmd.Parameters.Add(new SqlParameter("@TableName", weekTableName));
                cmd.Parameters.Add(new SqlParameter("@ExtractionDate", extractionDate.ToDateTime(TimeOnly.MinValue)));

                var rowsParam = new SqlParameter("@RowsAffected", System.Data.SqlDbType.Int)
                { Direction = System.Data.ParameterDirection.Output };
                cmd.Parameters.Add(rowsParam);

                await cmd.ExecuteNonQueryAsync(ct);

                int rows = rowsParam.Value != DBNull.Value ? (int)rowsParam.Value : 0;

                return new CleaningStepResultVm { Success = true, StepName = "CreateWeekTable", RowsAffected = rows };
            }
            catch (Exception ex)
            {
                return new CleaningStepResultVm { Success = false, StepName = "CreateWeekTable", Error = ex.Message };
            }
        }
        public async Task<bool> WeekTableExistsAsync(string weekTableName, CancellationToken ct)
        {
            var cs = _config.GetConnectionString("Section78Notice");
            if (string.IsNullOrWhiteSpace(cs)) return false;

            try
            {
                await using var conn = new SqlConnection(cs);
                await conn.OpenAsync(ct);

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
            SELECT COUNT(1)
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = 'dbo'
              AND TABLE_NAME   = @TableName";
                cmd.Parameters.Add(new SqlParameter("@TableName", weekTableName));

                var result = await cmd.ExecuteScalarAsync(ct);
                return Convert.ToInt32(result) > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}