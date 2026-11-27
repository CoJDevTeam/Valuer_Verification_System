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
        private readonly IWeekRowStore _weekRows;

        public ValuerDashboardService(VvsDbContext db, IWeekRowStore weekRows)
        {
            _db = db;
            _weekRows = weekRows;
        }

        public async Task<ValuerDashboardVm> BuildAsync(string valuer, CancellationToken ct)
        {
            valuer = (valuer ?? "").Trim();

            if (string.IsNullOrWhiteSpace(valuer))
                valuer = "UNKNOWN_VALUER";

            // The week table is the working table. Only batches the Project Manager approved are counted.
            var approved = await _weekRows.ApprovedBatchesAsync(ct);

            var map = new Dictionary<int, WeekCounts>();
            foreach (var b in approved)
            {
                var c = await _weekRows.CountsAsync(valuer, b.Id, ct);
                if (c.Total > 0) map[b.Id] = c;
            }

            var batches = approved.Where(b => map.ContainsKey(b.Id)).Take(50).ToList();

            var total = map.Values.Sum(x => x.Total);
            var pending = map.Values.Sum(x => x.Pending);
            var yes = map.Values.Sum(x => x.Yes);
            var no = map.Values.Sum(x => x.No);
            var yesToday = map.Values.Sum(x => x.YesToday);
            var noToday = map.Values.Sum(x => x.NoToday);
            var verifiedToday = yesToday + noToday;

            var verified = yes + no;
            var progress = total <= 0 ? 0m : Math.Round((decimal)verified * 100m / total, 2);

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
