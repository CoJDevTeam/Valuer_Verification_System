using Valuer_Verification_System.Models.ViewModels;

namespace Valuer_Verification_System.Services.Verifications
{
    public interface IValuerBulkVerificationService
    {
        Task<ValuerBulkInboxVm> GetInboxAsync(
            int? batchId,
            string username,
            CancellationToken ct);

        Task<ValuerBulkDetailsVm?> GetDetailsAsync(
         int batchId,
         string unitKey,
         string username,
         CancellationToken ct);

        Task<int> VerifyAllCorrectAsync(
            BulkVerifyRequestVm vm,
            string username,
            CancellationToken ct);
    }
}
