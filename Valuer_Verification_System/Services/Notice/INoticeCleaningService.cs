using Valuer_Verification_System.Models.ViewModels;

namespace Valuer_Verification_System.Services.Notice
{
    public interface INoticeCleaningService
    {
        /// <summary>
        /// Returns the auto-generated GV23_LIS_SV_YYYY_MM_EXTRACT_Wn table name
        /// based on the extraction date and week number in that month.
        /// </summary>
        string BuildWeekTableName(DateOnly extractionDate);

        /// <summary>Execute a single named cleaning step. Used by AJAX progress calls.</summary>
        Task<CleaningStepResultVm> RunStepAsync(
    int batchId,
    string stepName,
    DateOnly extractionDate,
    string lisMasterTable,
    string sapTable,
    CancellationToken ct);
        /// <summary>Create the clean week table on the 173 server and populate it.</summary>
        Task<CleaningStepResultVm> CreateWeekTableAsync(
       int batchId,
       string weekTableName,
       DateOnly extractionDate,
       CancellationToken ct);

        /// <summary>All step names in execution order.</summary>
        IReadOnlyList<string> StepNames { get; }


        Task<bool> WeekTableExistsAsync(string weekTableName, CancellationToken ct);

        Task<CleaningStepResultVm> RunFullCleaningOnceAsync(
    int batchId,
    DateOnly extractionDate,
    string lisMasterTable,
    string sapTable,
    CancellationToken ct);

        /// <summary>
        /// Phase 1 import: creates the week table with ALL raw rows of the batch
        /// (sp_VVS_ImportWeekTable) and runs the IMPORT cleaning steps on it
        /// (sector, valuer, trim). Stores the week table name on the batch.
        /// </summary>
        Task<WeekTableImportResult> ImportWeekTableAndCleanAsync(int batchId, string by, string lisTable, string sapTable, CancellationToken ct);

        /// <summary>
        /// Send day: runs the SEND cleaning steps in place on the week table
        /// (sp_VVS_WT_RunSteps @Phase = 'SEND'). Stops at the first failed step.
        /// </summary>
        Task<CleaningStepResultVm> RunSendDayCleaningAsync(int batchId, string by, string lisTable, string sapTable, CancellationToken ct);

        /// <summary>DEV/TEST: allocates N rows (after skipping "skip") of a week table to a developer/tester without touching Username. reset = clear earlier test allocations first.</summary>
        Task<int> DevAllocateAsync(string weekTable, string username, int count, int skip, bool reset, CancellationToken ct);

        Task<CleaningStepResultVm> InsertVerifiedRowsIntoWeekTableAsync(
    int batchId,
    string weekTableName,
    CancellationToken ct);
    }

    public sealed class WeekTableImportResult
    {
        public bool Success { get; set; }
        public string? WeekTableName { get; set; }
        public int RowsCopied { get; set; }
        public string Message { get; set; } = "";
    }
}
