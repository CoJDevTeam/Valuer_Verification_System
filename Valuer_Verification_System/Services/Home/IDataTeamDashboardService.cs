using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Models.Data;
using Valuer_Verification_System.Models.SLA;
using Valuer_Verification_System.Models.ViewModels;


namespace Valuer_Verification_System.Services.Home
{
    public interface IDataTeamDashboardService
    {
        Task<DataTeamDashboardVm> BuildAsync(CancellationToken ct);
    }

    public sealed class DataTeamDashboardService : IDataTeamDashboardService
    {
        private readonly VvsDbContext _db;

        public DataTeamDashboardService(VvsDbContext db)
        {
            _db = db;
        }

        public async Task<DataTeamDashboardVm> BuildAsync(CancellationToken ct)
        {
            // Latest week windows
            var weeks = await _db.WeekWindows
                .AsNoTracking()
                .OrderByDescending(w => w.WeekStart)
                .Take(10)
                .Select(w => new DataTeamDashboardVm.WeekWindowCardVm
                {
                    Id = w.Id,
                    WeekStart = w.WeekStart,
                    WeekEnd = w.WeekEnd,
                    NoticeSendDate = w.NoticeSendDate,
                    Status = w.Status
                })
                .ToListAsync(ct);

            // Latest batches
            var recentBatchIds = await _db.Batches
                .AsNoTracking()
                .OrderByDescending(b => b.Id)
                .Take(15)
                .Select(b => b.Id)
                .ToListAsync(ct);

            // Batch meta with week window info
            var batchesMeta = await _db.Batches
                .AsNoTracking()
                .Include(b => b.WeekWindow)
                .Where(b => recentBatchIds.Contains(b.Id))
                .OrderByDescending(b => b.Id)
                .ToListAsync(ct);

            // Counts per batch (single query)
            var counts = await _db.ExtractRows
                .AsNoTracking()
                .Where(r => recentBatchIds.Contains(r.BatchId))
                .GroupBy(r => r.BatchId)
                .Select(g => new
                {
                    BatchId = g.Key,
                    Total = g.Count(),
                    Pending = g.Count(x => x.Decision == VerificationDecision.Pending),
                    Yes = g.Count(x => x.Decision == VerificationDecision.Yes),
                    No = g.Count(x => x.Decision == VerificationDecision.No)
                })
                .ToListAsync(ct);

            var countMap = counts.ToDictionary(x => x.BatchId);

            var recentBatches = batchesMeta.Select(b =>
            {
                countMap.TryGetValue(b.Id, out var c);

                var total = c?.Total ?? 0;
                var pending = c?.Pending ?? 0;
                var yes = c?.Yes ?? 0;
                var no = c?.No ?? 0;
                var verified = yes + no;
                var progress = total <= 0 ? 0m : Math.Round((decimal)verified * 100m / total, 2);

                // ✅ SLA (traffic light)
                var sla = SlaCalculator.Evaluate(b.WeekWindow.NoticeSendDate, pending);

                return new DataTeamDashboardVm.BatchCardVm
                {
                    Id = b.Id,
                    BatchName = b.BatchName,
                    BatchDate = b.BatchDate,
                    WeekStart = b.WeekWindow.WeekStart,
                    WeekEnd = b.WeekWindow.WeekEnd,
                    NoticeSendDate = b.WeekWindow.NoticeSendDate,

                    Total = total,
                    Pending = pending,
                    Yes = yes,
                    No = no,
                    ProgressPercent = progress,

                    // ✅ SLA fields
                    SlaStatus = sla.Status,
                    DaysToSend = sla.DaysToSend,
                    SlaLabel = sla.Label
                };
            }).ToList();

            // Highlights
            var from30 = DateTime.UtcNow.AddDays(-30);
            var totalBatchesLast30 = await _db.Batches.AsNoTracking().CountAsync(b => b.CreatedAt >= from30, ct);
            var batchesInVerification = await _db.Batches.AsNoTracking().CountAsync(b => b.Status == BatchStatus.InVerification, ct);

            var vm = new DataTeamDashboardVm
            {
                WeekWindows = weeks,
                RecentBatches = recentBatches,
                ActiveWeekWindows = weeks.Count,
                TotalBatchesLast30 = totalBatchesLast30,
                BatchesInVerification = batchesInVerification
            };

            var latestWeek = weeks.FirstOrDefault();
            var latestBatch = recentBatches.FirstOrDefault();

            if (latestWeek == null)
            {
                vm.NextActionTitle = "Start Weekly Process";
                vm.NextActionHint = "Create a new Week Window (Mon–Sun) for the next extract.";
                vm.NextActionUrl = "/WeekWindows/Create";
                vm.NextActionButton = "Create Week Window";
            }
            else if (latestWeek.Status != WeekWindowStatus.Uploaded)
            {
                vm.NextActionTitle = "Upload Extract";
                vm.NextActionHint = $"Week {latestWeek.WeekStart:yyyy-MM-dd} → {latestWeek.WeekEnd:yyyy-MM-dd} is not uploaded yet.";
                vm.NextActionUrl = $"/ExtractUpload?weekWindowId={latestWeek.Id}";
                vm.NextActionButton = "Go to Upload";
            }
            else if (latestBatch != null && latestBatch.Pending > 0)
            {
                vm.NextActionTitle = "Verification In Progress";
                vm.NextActionHint = $"Latest batch {latestBatch.BatchName} has {latestBatch.Pending} pending records. Managers may need to reassign.";
                vm.NextActionUrl = $"/Manager/Dashboard?batchId={latestBatch.Id}";
                vm.NextActionButton = "Open Manager Dashboard";
            }
            else if (latestBatch != null && latestBatch.ReadyForNotices)
            {
                vm.NextActionTitle = "Batch Ready";
                vm.NextActionHint = $"Batch {latestBatch.BatchName} is fully verified. Generate weekly stats or proceed to notices (later module).";
                vm.NextActionUrl = $"/Manager/Stats/Download?batchId={latestBatch.Id}";
                vm.NextActionButton = "Download Stats";
            }
            else
            {
                vm.NextActionTitle = "Start Weekly Process";
                vm.NextActionHint = "Create the next Week Window or upload a new extract.";
                vm.NextActionUrl = "/WeekWindows";
                vm.NextActionButton = "Week Windows";
            }

            return vm;
        }
    }
}
