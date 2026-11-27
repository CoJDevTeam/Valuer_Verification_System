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

        // ── Phase 1: week table is the working table ─────────────────────────

        public async Task<WeekTableImportResult> ImportWeekTableAndCleanAsync(
            int batchId, string by, string lisTable, string sapTable, CancellationToken ct)
        {
            var batch = await _db.Batches.FirstOrDefaultAsync(x => x.Id == batchId, ct);
            if (batch is null)
                return new WeekTableImportResult { Message = "Batch not found." };

            // Already fully imported: nothing to do.
            if (!string.IsNullOrWhiteSpace(batch.CleanWeekTableName) && batch.ExtractCleanedAtUtc != null)
                return new WeekTableImportResult
                {
                    Success = true,
                    WeekTableName = batch.CleanWeekTableName,
                    Message = "Week table already exists for this batch."
                };

            batch.ExtractCleaningStatus = "Running";
            batch.ExtractCleaningMessage = "Creating week table.";
            batch.UpdatedBy = by;
            batch.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            string tableName;
            int copied;

            if (!string.IsNullOrWhiteSpace(batch.CleanWeekTableName))
            {
                // Table was created earlier but the steps failed: keep it, do not recreate.
                tableName = batch.CleanWeekTableName!;
                copied = await WeekTableRowCountAsync(tableName, ct);
            }
            else
            try
            {
                var conn = _db.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open)
                    await conn.OpenAsync(ct);

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "sp_VVS_ImportWeekTable";
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.CommandTimeout = 600;

                cmd.Parameters.Add(new SqlParameter("@BatchId", batchId));

                var nameParam = new SqlParameter("@TableName", System.Data.SqlDbType.NVarChar, 200)
                {
                    Direction = System.Data.ParameterDirection.InputOutput,
                    Value = DBNull.Value
                };
                var rowsParam = new SqlParameter("@RowsAffected", System.Data.SqlDbType.Int)
                {
                    Direction = System.Data.ParameterDirection.Output
                };
                cmd.Parameters.Add(nameParam);
                cmd.Parameters.Add(rowsParam);

                await cmd.ExecuteNonQueryAsync(ct);

                tableName = Convert.ToString(nameParam.Value) ?? "";
                copied = rowsParam.Value != DBNull.Value ? Convert.ToInt32(rowsParam.Value) : 0;
            }
            catch (Exception ex)
            {
                batch.ExtractCleaningStatus = "Failed";
                batch.ExtractCleaningMessage = $"Week table creation failed: {ex.Message}";
                await _db.SaveChangesAsync(ct);
                return new WeekTableImportResult { Message = batch.ExtractCleaningMessage };
            }

            var rawCount = await _db.ExtractRows.CountAsync(r => r.BatchId == batchId, ct);
            if (copied != rawCount)
            {
                batch.ExtractCleaningStatus = "Failed";
                batch.ExtractCleaningMessage =
                    $"Week table {tableName} has {copied} rows but the raw extract has {rawCount}. Not released to valuers.";
                batch.CleanWeekTableName = tableName;
                await _db.SaveChangesAsync(ct);
                return new WeekTableImportResult
                {
                    WeekTableName = tableName,
                    RowsCopied = copied,
                    Message = batch.ExtractCleaningMessage
                };
            }

            batch.CleanWeekTableName = tableName;
            await _db.SaveChangesAsync(ct);

            var steps = await RunWeekStepsAsync(batch, tableName, "IMPORT", by, lisTable, sapTable, ct);
            if (!steps.Success)
                return new WeekTableImportResult
                {
                    WeekTableName = tableName,
                    RowsCopied = copied,
                    Message = steps.Error ?? "Import cleaning failed."
                };

            batch.ExtractCleanedAtUtc = DateTime.UtcNow;
            batch.ExtractCleanedBy = by;
            batch.ExtractCleaningStatus = "Cleaned";
            batch.ExtractCleaningMessage =
                $"Week table {tableName} created with {copied:N0} rows. Import steps done (sector, valuer, trim). Send-day cleaning pending.";
            batch.UpdatedBy = by;
            batch.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            return new WeekTableImportResult
            {
                Success = true,
                WeekTableName = tableName,
                RowsCopied = copied,
                Message = batch.ExtractCleaningMessage
            };
        }

        private async Task<int> WeekTableRowCountAsync(string tableName, CancellationToken ct)
        {
            tableName = CleanTableName(tableName);
            var conn = _db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM [SECTION_78_NOTICE].[dbo].[{tableName}]";
            return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        }

        public async Task<CleaningStepResultVm> RunSendDayCleaningAsync(
            int batchId, string by, string lisTable, string sapTable, CancellationToken ct)
        {
            var batch = await _db.Batches.FirstOrDefaultAsync(x => x.Id == batchId, ct);
            if (batch is null || string.IsNullOrWhiteSpace(batch.CleanWeekTableName))
                return new CleaningStepResultVm
                {
                    StepName = "SendDayCleaning",
                    Error = "Batch has no week table."
                };

            var result = await RunWeekStepsAsync(batch, batch.CleanWeekTableName!, "SEND", by, lisTable, sapTable, ct);

            if (result.Success)
            {
                batch.ExtractCleaningStatus = "ReadyToSend";
                batch.ExtractCleaningMessage = $"Send-day cleaning complete on {batch.CleanWeekTableName}.";
            }
            else
            {
                batch.ExtractCleaningStatus = "Failed";
                batch.ExtractCleaningMessage = $"Send-day cleaning failed: {result.Error}";
            }
            batch.UpdatedBy = by;
            batch.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            return result;
        }

        public async Task<int> DevAllocateAsync(string weekTable, string username, int count, int skip, bool reset, CancellationToken ct)
        {
            var conn = _db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "sp_VVS_WT_DevAllocate";
            cmd.CommandType = System.Data.CommandType.StoredProcedure;
            cmd.CommandTimeout = 120;
            cmd.Parameters.Add(new SqlParameter("@WeekTable", weekTable));
            cmd.Parameters.Add(new SqlParameter("@Username", username));
            cmd.Parameters.Add(new SqlParameter("@Count", count));
            cmd.Parameters.Add(new SqlParameter("@Skip", skip));
            cmd.Parameters.Add(new SqlParameter("@Reset", reset));
            var rows = new SqlParameter("@RowsAffected", System.Data.SqlDbType.Int)
            { Direction = System.Data.ParameterDirection.Output };
            cmd.Parameters.Add(rows);
            await cmd.ExecuteNonQueryAsync(ct);
            return rows.Value != DBNull.Value ? Convert.ToInt32(rows.Value) : 0;
        }

        // Runs sp_VVS_WT_RunSteps and writes one CleaningStepAudit row per step.
        private async Task<CleaningStepResultVm> RunWeekStepsAsync(
            Batch batch, string weekTable, string phase, string by, string lisTable, string sapTable, CancellationToken ct)
        {
            var total = 0;
            try
            {
                var conn = _db.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open)
                    await conn.OpenAsync(ct);

                lisTable = CleanTableName(lisTable);
                sapTable = CleanTableName(sapTable);

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "sp_VVS_WT_RunSteps";
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.CommandTimeout = 1800;

                cmd.Parameters.Add(new SqlParameter("@WeekTable", weekTable));
                cmd.Parameters.Add(new SqlParameter("@BatchId", batch.Id));
                cmd.Parameters.Add(new SqlParameter("@Phase", phase));
                cmd.Parameters.Add(new SqlParameter("@ExtractionDate", batch.BatchDate.ToDateTime(TimeOnly.MinValue)));
                cmd.Parameters.Add(new SqlParameter("@LisMasterTable", lisTable));
                cmd.Parameters.Add(new SqlParameter("@SapTable", sapTable));

                var rows = new List<(string Step, int Rows, string Status, string? Error, decimal Secs)>();

                await using (var rd = await cmd.ExecuteReaderAsync(ct))
                {
                    while (await rd.ReadAsync(ct))
                    {
                        rows.Add((
                            rd.GetString(0),
                            rd.IsDBNull(1) ? 0 : rd.GetInt32(1),
                            rd.GetString(2),
                            rd.IsDBNull(3) ? null : rd.GetString(3),
                            rd.IsDBNull(4) ? 0 : rd.GetDecimal(4)));
                    }
                }

                foreach (var r in rows)
                {
                    var ok = r.Status.Equals("OK", StringComparison.OrdinalIgnoreCase);
                    _db.CleaningStepAudits.Add(new CleaningStepAudit
                    {
                        BatchId = batch.Id,
                        StepName = $"{phase}:{r.Step}",
                        StartedAtUtc = DateTime.UtcNow,
                        FinishedAtUtc = DateTime.UtcNow,
                        DurationMs = (long)(r.Secs * 1000),
                        Success = ok,
                        RowsAffected = r.Rows,
                        Message = ok ? $"{r.Step} complete. Rows affected: {r.Rows:N0}." : $"{r.Step} failed.",
                        ErrorMessage = ok ? null : r.Error,
                        CreatedAt = DateTime.UtcNow
                    });
                    if (ok) total += r.Rows;
                }
                await _db.SaveChangesAsync(ct);

                var failed = rows.FirstOrDefault(r => !r.Status.Equals("OK", StringComparison.OrdinalIgnoreCase));
                if (failed.Step is not null)
                    return new CleaningStepResultVm
                    {
                        StepName = failed.Step,
                        RowsAffected = total,
                        Error = $"Step '{failed.Step}' failed: {failed.Error}"
                    };

                if (rows.Count == 0)
                    return new CleaningStepResultVm { StepName = phase, Error = "No step results were returned." };

                return new CleaningStepResultVm { Success = true, StepName = phase, RowsAffected = total };
            }
            catch (Exception ex)
            {
                batch.ExtractCleaningStatus = "Failed";
                batch.ExtractCleaningMessage = $"{phase} cleaning failed: {ex.Message}";
                await _db.SaveChangesAsync(ct);
                return new CleaningStepResultVm { StepName = phase, RowsAffected = total, Error = ex.Message };
            }
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