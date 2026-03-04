using Valuer_Verification_System.Models.Data;
using Valuer_Verification_System.Models.Notice;

namespace Valuer_Verification_System.Services.Notice
{
   public interface INoticePdfService
    {
        /// <summary>
        /// Generates a notice PDF.
        /// For multipurpose properties: splitRows holds up to 4 split records (grouped by ValuationKey).
        /// For single properties:       splitRows has exactly 1 item (the main row).
        /// </summary>
        byte[] GenerateNotice(ExtractRow mainRow, NoticeContext ctx, List<ExtractRow> splitRows);

        /// <summary>
        /// Preview: generates a notice for the first sendable property in a batch.
        /// Returns null if no sendable rows exist.
        /// </summary>
        Task<byte[]?> GeneratePreviewAsync(int batchId, NoticeContext ctx,
            Microsoft.EntityFrameworkCore.DbContext db, CancellationToken ct);
    }
}
