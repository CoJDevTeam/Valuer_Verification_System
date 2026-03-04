using Valuer_Verification_System.Models.ViewModels;

namespace Valuer_Verification_System.Services.Notice
{
    public interface INoticePrintService
    {
        /// <summary>
        /// Builds the output folder path:
        /// {RootPath}/Notices/{MMMM yyyy}/{batchName}/{weekTableName}
        /// </summary>
        string BuildOutputFolder(DateOnly extractionDate, string batchName, string weekTableName);

        /// <summary>Total distinct ValuationKey groups to print (SendNotice=Y, Decision=Yes).</summary>
        Task<int> GetTotalNoticeCountAsync(int batchId, CancellationToken ct);

        /// <summary>
        /// Prints a chunk of notices starting at offset.
        /// Returns progress for AJAX polling.
        /// </summary>
        Task<PrintChunkResult> PrintChunkAsync(
            int batchId, string weekTableName,
            int offset, int chunkSize,
            CancellationToken ct);
    }
}
