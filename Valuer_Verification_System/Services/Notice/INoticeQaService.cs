using Valuer_Verification_System.Models.Data;
using Valuer_Verification_System.Models.ViewModels;

namespace Valuer_Verification_System.Services.Notice
{
    public interface INoticeQaService
    {
        Task<NoticeQaVm> BuildQaAsync(int batchId, string? weekTableName, CancellationToken ct);

        Task<bool> IsApprovedAsync(int batchId, CancellationToken ct);

        Task<NoticeQaApproval?> GetApprovalAsync(int batchId, CancellationToken ct);

        Task<string?> GetSamplePdfPathAsync(int sampleId, CancellationToken ct);

        Task<NoticeQaApproval> ApproveAsync(
            int batchId,
            string? comments,
            CancellationToken ct);
    }
}