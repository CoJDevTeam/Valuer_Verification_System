using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Models.Data;

namespace Valuer_Verification_System.Services.Notice
{
    public interface INoticeGuardService
    {
        Task EnsureBatchReadyForNoticesAsync(int batchId, CancellationToken ct);
        Task<int> GetPendingCountAsync(int batchId, CancellationToken ct);
    }

    public sealed class NoticeGuardService : INoticeGuardService
    {
        private readonly VvsDbContext _db;

        public NoticeGuardService(VvsDbContext db)
        {
            _db = db;
        }

        public Task<int> GetPendingCountAsync(int batchId, CancellationToken ct)
        {
            return _db.ExtractRows
                .AsNoTracking()
                .CountAsync(r => r.BatchId == batchId && r.Decision == VerificationDecision.Pending, ct);
        }

        public async Task EnsureBatchReadyForNoticesAsync(int batchId, CancellationToken ct)
        {
            if (batchId <= 0)
                throw new InvalidOperationException("Batch is required.");

            var pending = await GetPendingCountAsync(batchId, ct);

            if (pending > 0)
                throw new InvalidOperationException($"Notices cannot be generated/sent. Pending verifications: {pending}.");
        }
    }
}
