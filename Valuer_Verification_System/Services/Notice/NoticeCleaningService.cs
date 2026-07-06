using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Models.Data;
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
            "SectorValuer",
            "UnitType",
            "PremiseId",
            "ErfPtnRe",
            "Dates",
            "SchemeInfo",
            "PropertyDescription",
            "SapAddresses",
            "LisStreetAddress",
            "UsableFlags",
            "CojOwned",
            "FallbackAddresses",
            "FinalMappings",
            "ResolveUsername",
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
           int batchId,
           string stepName,
           DateOnly extractionDate,
           string lisMasterTable,
           string sapTable,
           CancellationToken ct)
        {
            if (batchId <= 0)
            {
                return new CleaningStepResultVm
                {
                    Success = false,
                    StepName = stepName,
                    Error = "BatchId is required."
                };
            }

            if (string.IsNullOrWhiteSpace(stepName))
            {
                return new CleaningStepResultVm
                {
                    Success = false,
                    StepName = "Unknown",
                    Error = "Step name is required."
                };
            }

            lisMasterTable = CleanTableName(lisMasterTable);
            sapTable = CleanTableName(sapTable);

            // SAP is handled by the new batch-based SAP procedure.
            if (stepName.Equals("SapAddresses", StringComparison.OrdinalIgnoreCase))
            {
                return await RunSapAddressesStepAsync(
                    batchId,
                    sapTable,
                    ct);
            }

            var spName = $"sp_VVS_Clean_{stepName}";

            try
            {
                var conn = _db.Database.GetDbConnection();

                if (conn.State != System.Data.ConnectionState.Open)
                    await conn.OpenAsync(ct);

                await using var cmd = conn.CreateCommand();

                cmd.CommandText = spName;
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.CommandTimeout = 600;

                cmd.Parameters.Add(new SqlParameter("@BatchId", batchId));
                cmd.Parameters.Add(new SqlParameter("@ExtractionDate", extractionDate.ToDateTime(TimeOnly.MinValue)));
                cmd.Parameters.Add(new SqlParameter("@LisMasterTable", lisMasterTable));
                cmd.Parameters.Add(new SqlParameter("@SapTable", sapTable));

                var rowsParam = new SqlParameter("@RowsAffected", System.Data.SqlDbType.Int)
                {
                    Direction = System.Data.ParameterDirection.Output
                };

                cmd.Parameters.Add(rowsParam);

                await cmd.ExecuteNonQueryAsync(ct);

                var rows = rowsParam.Value != DBNull.Value
                    ? Convert.ToInt32(rowsParam.Value)
                    : 0;

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

        private async Task<CleaningStepResultVm> RunSapAddressesStepAsync(
           int batchId,
           string sapTable,
           CancellationToken ct)
        {
            sapTable = CleanTableName(sapTable);

            try
            {
                var conn = _db.Database.GetDbConnection();

                if (conn.State != System.Data.ConnectionState.Open)
                    await conn.OpenAsync(ct);

                await using var cmd = conn.CreateCommand();

                cmd.CommandText = "sp_VVS_Clean_SapAddresses_ByBatch";
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.CommandTimeout = 600;

                cmd.Parameters.Add(new SqlParameter("@BatchId", batchId));
                cmd.Parameters.Add(new SqlParameter("@SapTable", sapTable));

                var rowsParam = new SqlParameter("@RowsAffected", System.Data.SqlDbType.Int)
                {
                    Direction = System.Data.ParameterDirection.Output
                };

                cmd.Parameters.Add(rowsParam);

                await cmd.ExecuteNonQueryAsync(ct);

                var rows = rowsParam.Value != DBNull.Value
                    ? Convert.ToInt32(rowsParam.Value)
                    : 0;

                return new CleaningStepResultVm
                {
                    Success = true,
                    StepName = "SapAddresses",
                    RowsAffected = rows
                };
            }
            catch (Exception ex)
            {
                return new CleaningStepResultVm
                {
                    Success = false,
                    StepName = "SapAddresses",
                    Error = ex.Message
                };
            }
        }
        // ── Create week table on 118 server · SECTION_78_NOTICE ──────────────

        public async Task<CleaningStepResultVm> CreateWeekTableAsync(
    int batchId,
    string weekTableName,
    DateOnly extractionDate,
    CancellationToken ct)
        {
            var cs = _config.GetConnectionString("Section78Notice");

            if (string.IsNullOrWhiteSpace(cs))
            {
                return new CleaningStepResultVm
                {
                    Success = false,
                    StepName = "CreateWeekTable",
                    Error = "Connection string 'Section78Notice' not found in appsettings."
                };
            }

            if (batchId <= 0)
            {
                return new CleaningStepResultVm
                {
                    Success = false,
                    StepName = "CreateWeekTable",
                    Error = "BatchId is required."
                };
            }

            if (string.IsNullOrWhiteSpace(weekTableName))
            {
                return new CleaningStepResultVm
                {
                    Success = false,
                    StepName = "CreateWeekTable",
                    Error = "Week table name is required."
                };
            }

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

                // ✅ New parameter
                cmd.Parameters.Add(new SqlParameter("@BatchId", batchId));

                var rowsParam = new SqlParameter("@RowsAffected", System.Data.SqlDbType.Int)
                {
                    Direction = System.Data.ParameterDirection.Output
                };
                cmd.Parameters.Add(rowsParam);

                await cmd.ExecuteNonQueryAsync(ct);

                var rows = rowsParam.Value != DBNull.Value
                    ? Convert.ToInt32(rowsParam.Value)
                    : 0;

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
        private static string CleanTableName(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new InvalidOperationException("Table name is missing.");

            tableName = tableName.Trim();

            if (tableName.StartsWith("dbo.", StringComparison.OrdinalIgnoreCase))
                tableName = tableName.Substring(4);

            tableName = tableName.Replace("[", "").Replace("]", "").Trim();

            if (tableName.Contains(";") || tableName.Contains("--") || tableName.Contains(" "))
                throw new InvalidOperationException($"Invalid table name '{tableName}'.");

            return tableName;
        }
        public async Task<CleaningStepResultVm> RunFullCleaningOnceAsync(
       int batchId,
       DateOnly extractionDate,
       string lisMasterTable,
       string sapTable,
       CancellationToken ct)
        {
            lisMasterTable = CleanTableName(lisMasterTable);
            sapTable = CleanTableName(sapTable);

            var batch = await _db.Batches
                .FirstOrDefaultAsync(x => x.Id == batchId, ct);

            if (batch is null)
            {
                return new CleaningStepResultVm
                {
                    Success = false,
                    StepName = "FullCleaning",
                    Error = "Batch not found."
                };
            }

            if (batch.ExtractCleanedAtUtc != null)
            {
                return new CleaningStepResultVm
                {
                    Success = true,
                    StepName = "FullCleaning",
                    RowsAffected = 0,
                    Error = $"Skipped. Batch was already cleaned on {batch.ExtractCleanedAtUtc:yyyy-MM-dd HH:mm} UTC."
                };
            }

            var weekTableName = BuildWeekTableName(extractionDate);

            batch.ExtractCleaningStatus = "Running";
            batch.ExtractCleaningMessage = "Cleaning started by folder job.";
            batch.UpdatedBy = "folder-job";
            batch.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);

            var totalRowsAffected = 0;
            var messages = new List<string>();

            foreach (var step in StepNames)
            {
                var startedAt = DateTime.UtcNow;

                var audit = new CleaningStepAudit
                {
                    BatchId = batchId,
                    StepName = step,
                    StartedAtUtc = startedAt,
                    Success = false,
                    RowsAffected = 0,
                    Message = $"{step} started.",
                    CreatedAt = DateTime.UtcNow
                };

                _db.CleaningStepAudits.Add(audit);
                await _db.SaveChangesAsync(ct);

                var sw = System.Diagnostics.Stopwatch.StartNew();

                var stepResult = await RunStepAsync(
                    batchId,
                    step,
                    extractionDate,
                    lisMasterTable,
                    sapTable,
                    ct);

                sw.Stop();

                audit.FinishedAtUtc = DateTime.UtcNow;
                audit.DurationMs = sw.ElapsedMilliseconds;
                audit.Success = stepResult.Success;
                audit.RowsAffected = stepResult.RowsAffected;

                if (stepResult.Success)
                {
                    audit.Message =
                        $"{step} complete. Rows affected: {stepResult.RowsAffected:N0}. Duration: {FormatDuration(sw.ElapsedMilliseconds)}.";
                }
                else
                {
                    audit.Message = $"{step} failed.";
                    audit.ErrorMessage = stepResult.Error;
                }

                await _db.SaveChangesAsync(ct);

                if (!stepResult.Success)
                {
                    batch.ExtractCleaningStatus = "Failed";
                    batch.ExtractCleaningMessage = $"Cleaning failed at step '{step}': {stepResult.Error}";
                    batch.UpdatedBy = "folder-job";
                    batch.UpdatedAt = DateTime.UtcNow;

                    await _db.SaveChangesAsync(ct);

                    return new CleaningStepResultVm
                    {
                        Success = false,
                        StepName = step,
                        RowsAffected = totalRowsAffected,
                        Error = batch.ExtractCleaningMessage
                    };
                }

                totalRowsAffected += stepResult.RowsAffected;
                messages.Add($"{step}: {stepResult.RowsAffected:N0} in {FormatDuration(sw.ElapsedMilliseconds)}");
            }

            var createStartedAt = DateTime.UtcNow;

            var createAudit = new CleaningStepAudit
            {
                BatchId = batchId,
                StepName = "CreateWeekTable",
                StartedAtUtc = createStartedAt,
                Success = false,
                RowsAffected = 0,
                Message = "CreateWeekTable started.",
                CreatedAt = DateTime.UtcNow
            };

            _db.CleaningStepAudits.Add(createAudit);
            await _db.SaveChangesAsync(ct);

            var createSw = System.Diagnostics.Stopwatch.StartNew();

            var createTableResult = await CreateWeekTableAsync(
                batchId,
                weekTableName,
                extractionDate,
                ct);

            createSw.Stop();

            createAudit.FinishedAtUtc = DateTime.UtcNow;
            createAudit.DurationMs = createSw.ElapsedMilliseconds;
            createAudit.Success = createTableResult.Success;
            createAudit.RowsAffected = createTableResult.RowsAffected;
            createAudit.Message = createTableResult.Success
                ? $"CreateWeekTable complete. Rows affected: {createTableResult.RowsAffected:N0}. Duration: {FormatDuration(createSw.ElapsedMilliseconds)}."
                : "CreateWeekTable failed.";
            createAudit.ErrorMessage = createTableResult.Success ? null : createTableResult.Error;

            await _db.SaveChangesAsync(ct);
            if (!createTableResult.Success)
            {
                batch.ExtractCleaningStatus = "Failed";
                batch.ExtractCleaningMessage = $"Week table creation failed: {createTableResult.Error}";
                batch.UpdatedBy = "folder-job";
                batch.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync(ct);

                return new CleaningStepResultVm
                {
                    Success = false,
                    StepName = "CreateWeekTable",
                    RowsAffected = totalRowsAffected,
                    Error = batch.ExtractCleaningMessage
                };
            }

            totalRowsAffected += createTableResult.RowsAffected;
            messages.Add($"CreateWeekTable: {createTableResult.RowsAffected:N0}");

            batch.ExtractCleanedAtUtc = DateTime.UtcNow;
            batch.ExtractCleanedBy = "folder-job";
            batch.CleanWeekTableName = weekTableName;
            batch.ExtractCleaningStatus = "Cleaned";
            batch.ExtractCleaningMessage = string.Join(" | ", messages);
            batch.Status = BatchStatus.Uploaded;
            batch.UpdatedBy = "folder-job";
            batch.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);

            return new CleaningStepResultVm
            {
                Success = true,
                StepName = "FullCleaning",
                RowsAffected = totalRowsAffected,
                Error = batch.ExtractCleaningMessage
            };
        }

        private static string FormatDuration(long milliseconds)
        {
            var ts = TimeSpan.FromMilliseconds(milliseconds);

            if (ts.TotalSeconds < 1)
                return $"{milliseconds} ms";

            if (ts.TotalMinutes < 1)
                return $"{ts.TotalSeconds:N1} sec";

            return $"{ts.TotalMinutes:N1} min";
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
        public async Task<CleaningStepResultVm> InsertVerifiedRowsIntoWeekTableAsync(
    int batchId,
    string weekTableName,
    CancellationToken ct)
        {
            weekTableName = CleanTableName(weekTableName);

            var batch = await _db.Batches
                .AsNoTracking()
                .Include(x => x.WeekWindow)
                .FirstOrDefaultAsync(x => x.Id == batchId, ct);

            if (batch is null)
            {
                return new CleaningStepResultVm
                {
                    Success = false,
                    StepName = "InsertWeekTable",
                    Error = "Batch not found."
                };
            }

           

            if (string.IsNullOrWhiteSpace(batch.CleanWeekTableName))
            {
                return new CleaningStepResultVm
                {
                    Success = false,
                    StepName = "InsertWeekTable",
                    Error = "Batch does not have a clean week table name."
                };
            }

            if (!string.Equals(
                    CleanTableName(batch.CleanWeekTableName),
                    weekTableName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return new CleaningStepResultVm
                {
                    Success = false,
                    StepName = "InsertWeekTable",
                    Error = $"Requested week table '{weekTableName}' does not match batch clean week table '{batch.CleanWeekTableName}'."
                };
            }

            var pending = await _db.ExtractRows
                .AsNoTracking()
                .CountAsync(x =>
                    x.BatchId == batchId &&
                    x.Decision == VerificationDecision.Pending,
                    ct);

            if (pending > 0)
            {
                return new CleaningStepResultVm
                {
                    Success = false,
                    StepName = "InsertWeekTable",
                    Error = $"Cannot insert into week table. There are still {pending:N0} pending verification records."
                };
            }

            var total = await _db.ExtractRows
                .AsNoTracking()
                .CountAsync(x => x.BatchId == batchId, ct);

            if (total <= 0)
            {
                return new CleaningStepResultVm
                {
                    Success = false,
                    StepName = "InsertWeekTable",
                    Error = "No extract rows found for this batch."
                };
            }

            var cs = _config.GetConnectionString("Section78Notice");

            if (string.IsNullOrWhiteSpace(cs))
            {
                return new CleaningStepResultVm
                {
                    Success = false,
                    StepName = "InsertWeekTable",
                    Error = "Connection string 'Section78Notice' not found in appsettings."
                };
            }

            try
            {
                await using var conn = new SqlConnection(cs);
                await conn.OpenAsync(ct);

                await using var cmd = conn.CreateCommand();

                cmd.CommandText = "sp_VVS_InsertVerifiedRowsIntoWeekTable";
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.CommandTimeout = 900;

                cmd.Parameters.Add(new SqlParameter("@BatchId", batchId));
                cmd.Parameters.Add(new SqlParameter("@TableName", weekTableName));

                var rowsParam = new SqlParameter("@RowsAffected", System.Data.SqlDbType.Int)
                {
                    Direction = System.Data.ParameterDirection.Output
                };

                cmd.Parameters.Add(rowsParam);

                await cmd.ExecuteNonQueryAsync(ct);

                var rows = rowsParam.Value != DBNull.Value
                    ? Convert.ToInt32(rowsParam.Value)
                    : 0;

                return new CleaningStepResultVm
                {
                    Success = true,
                    StepName = "InsertWeekTable",
                    RowsAffected = rows,
                    Error = $"Inserted/refreshed {rows:N0} verified rows into week table '{weekTableName}'."
                };
            }
            catch (Exception ex)
            {
                return new CleaningStepResultVm
                {
                    Success = false,
                    StepName = "InsertWeekTable",
                    Error = ex.Message
                };
            }
        }
    }
}