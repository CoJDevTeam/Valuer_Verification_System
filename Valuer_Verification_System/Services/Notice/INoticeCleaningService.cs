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
            string stepName,
            DateOnly extractionDate,
            string lisMasterTable,
            string sapTable,
            CancellationToken ct);

        /// <summary>Create the clean week table on the 173 server and populate it.</summary>
        Task<CleaningStepResultVm> CreateWeekTableAsync(
            string weekTableName,
            DateOnly extractionDate,
            CancellationToken ct);

        /// <summary>All step names in execution order.</summary>
        IReadOnlyList<string> StepNames { get; }
    }
}
