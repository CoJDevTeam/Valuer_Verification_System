using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Models.Data;
using Valuer_Verification_System.Models.ViewModels;

namespace Valuer_Verification_System.Services.Valuer
{

    public interface IValuerDashboardService
    {
        Task<ValuerDashboardVm> BuildAsync(string valuer, CancellationToken ct);
    }

    public sealed class ValuerDashboardService : IValuerDashboardService
    {
        private readonly VvsDbContext _db;

        public ValuerDashboardService(VvsDbContext db)
        {
            _db = db;
        }

        public async Task<ValuerDashboardVm> BuildAsync(string valuer, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(valuer))
                valuer = "UNKNOWN_VALUER";

            // Base query: rows assigned to this valuer (current assignment)
            var assignedRowsQ = _db.Assignments
                .AsNoTracking()
                .Where(a => a.AssignedToValuer == valuer)
                .Select(a => a.ExtractRow);

            var total = await assignedRowsQ.CountAsync(ct);
            var pending = await assignedRowsQ.CountAsync(r => r.Decision == VerificationDecision.Pending, ct);
            var yes = await assignedRowsQ.CountAsync(r => r.Decision == VerificationDecision.Yes, ct);
            var no = await assignedRowsQ.CountAsync(r => r.Decision == VerificationDecision.No, ct);

            var verified = yes + no;
            var progress = total <= 0 ? 0m : Math.Round((decimal)verified * 100m / total, 2);

            // Today performance (UTC - matches DecisionAtUtc usage)
            var todayUtc = DateTime.UtcNow.Date;

            var verifiedToday = await assignedRowsQ.CountAsync(r =>
                r.DecisionAtUtc != null &&
                r.DecisionAtUtc.Value >= todayUtc &&
                (r.Decision == VerificationDecision.Yes || r.Decision == VerificationDecision.No), ct);

            var yesToday = await assignedRowsQ.CountAsync(r =>
                r.DecisionAtUtc != null &&
                r.DecisionAtUtc.Value >= todayUtc &&
                r.Decision == VerificationDecision.Yes, ct);

            var noToday = await assignedRowsQ.CountAsync(r =>
                r.DecisionAtUtc != null &&
                r.DecisionAtUtc.Value >= todayUtc &&
                r.Decision == VerificationDecision.No, ct);

            // My batches summary
            var myBatchIds = await _db.Assignments
                .AsNoTracking()
                .Where(a => a.AssignedToValuer == valuer)
                .Select(a => a.ExtractRow.BatchId)
                .Distinct()
                .OrderByDescending(x => x)
                .Take(50)
                .ToListAsync(ct);

            var batches = await _db.Batches
                .AsNoTracking()
                .Include(b => b.WeekWindow)
                .Where(b => myBatchIds.Contains(b.Id))
                .OrderByDescending(b => b.Id)
                .ToListAsync(ct);

            // For performance: aggregate counts by batch in one query
            var countsByBatch = await _db.Assignments
                .AsNoTracking()
                .Where(a => a.AssignedToValuer == valuer)
                .GroupBy(a => a.ExtractRow.BatchId)
                .Select(g => new
                {
                    BatchId = g.Key,
                    Total = g.Count(),
                    Pending = g.Count(x => x.ExtractRow.Decision == VerificationDecision.Pending),
                    Yes = g.Count(x => x.ExtractRow.Decision == VerificationDecision.Yes),
                    No = g.Count(x => x.ExtractRow.Decision == VerificationDecision.No),
                })
                .ToListAsync(ct);

            var map = countsByBatch.ToDictionary(x => x.BatchId);

            var batchVms = batches.Select(b =>
            {
                map.TryGetValue(b.Id, out var c);
                var bt = c?.Total ?? 0;
                var by = c?.Yes ?? 0;
                var bn = c?.No ?? 0;
                var bp = c?.Pending ?? 0;
                var bProg = bt <= 0 ? 0m : Math.Round((decimal)(by + bn) * 100m / bt, 2);

                return new ValuerDashboardVm.BatchSummaryVm
                {
                    BatchId = b.Id,
                    BatchName = b.BatchName,
                    BatchDate = b.BatchDate,
                    WeekStart = b.WeekWindow.WeekStart,
                    WeekEnd = b.WeekWindow.WeekEnd,
                    NoticeSendDate = b.WeekWindow.NoticeSendDate,
                    Total = bt,
                    Pending = bp,
                    Yes = by,
                    No = bn,
                    ProgressPercent = bProg
                };
            }).ToList();

            return new ValuerDashboardVm
            {
                ValuerName = valuer,
                TotalAssigned = total,
                Pending = pending,
                Yes = yes,
                No = no,
                ProgressPercent = progress,
                VerifiedToday = verifiedToday,
                YesToday = yesToday,
                NoToday = noToday,
                MyBatches = batchVms
            };
        }
    }
}
