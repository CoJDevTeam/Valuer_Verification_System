using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Models.Data;
using Valuer_Verification_System.Models.SLA;
using Valuer_Verification_System.Models.ViewModels;


namespace Valuer_Verification_System.Services.Manager
{
    public interface IManagerDashboardService
    {
        Task<ManagerDashboardVm> BuildAsync(int? batchId, CancellationToken ct);
    }

    public sealed class ManagerDashboardService : IManagerDashboardService
    {
        private readonly VvsDbContext _db;
        
        public ManagerDashboardService(VvsDbContext db)
        {
            _db = db;
        }

        public async Task<ManagerDashboardVm> BuildAsync(int? batchId, CancellationToken ct)
        {
            // Dropdown batches
            var batches = await _db.Batches
                .AsNoTracking()
                .OrderByDescending(b => b.Id)
                .Take(200)
                .Select(b => new ManagerDashboardVm.BatchOptionVm
                {
                    Id = b.Id,
                    BatchName = b.BatchName,
                    BatchDate = b.BatchDate
                })
                .ToListAsync(ct);

            var selectedBatchId = batchId ?? batches.FirstOrDefault()?.Id ?? 0;
            if (selectedBatchId <= 0)
            {
                return new ManagerDashboardVm
                {
                    BatchId = 0,
                    Batches = batches
                };
            }

            // Batch + week data
            var batch = await _db.Batches
                .AsNoTracking()
                .Include(b => b.WeekWindow)
                .FirstOrDefaultAsync(b => b.Id == selectedBatchId, ct);

            if (batch is null)
                throw new InvalidOperationException("Batch not found.");

            // Overall counts
            var total = await _db.ExtractRows.AsNoTracking().CountAsync(r => r.BatchId == selectedBatchId, ct);
            var pending = await _db.ExtractRows.AsNoTracking().CountAsync(r => r.BatchId == selectedBatchId && r.Decision == VerificationDecision.Pending, ct);
            var yes = await _db.ExtractRows.AsNoTracking().CountAsync(r => r.BatchId == selectedBatchId && r.Decision == VerificationDecision.Yes, ct);
            var no = await _db.ExtractRows.AsNoTracking().CountAsync(r => r.BatchId == selectedBatchId && r.Decision == VerificationDecision.No, ct);

            var verified = yes + no;
            var progress = total <= 0 ? 0m : Math.Round((decimal)verified * 100m / total, 2);

            // ✅ SLA
            var sla = SlaCalculator.Evaluate(batch.WeekWindow.NoticeSendDate, pending);

            // Breakdown by valuer (based on current assignment)
            var byValuerRaw = await _db.Assignments
                .AsNoTracking()
                .Where(a => a.ExtractRow.BatchId == selectedBatchId)
                .GroupBy(a => a.AssignedToValuer)
                .Select(g => new
                {
                    Valuer = g.Key,
                    Total = g.Count(),
                    Pending = g.Count(x => x.ExtractRow.Decision == VerificationDecision.Pending),
                    Yes = g.Count(x => x.ExtractRow.Decision == VerificationDecision.Yes),
                    No = g.Count(x => x.ExtractRow.Decision == VerificationDecision.No)
                })
                .OrderByDescending(x => x.Pending) // who is holding up the batch
                .ThenBy(x => x.Valuer)
                .ToListAsync(ct);

            var byValuer = byValuerRaw.Select(x =>
            {
                var vVerified = x.Yes + x.No;
                var vProgress = x.Total <= 0 ? 0m : Math.Round((decimal)vVerified * 100m / x.Total, 2);

                return new ManagerDashboardVm.ValuerBreakdownVm
                {
                    Valuer = x.Valuer,
                    Total = x.Total,
                    Pending = x.Pending,
                    Yes = x.Yes,
                    No = x.No,
                    ProgressPercent = vProgress
                };
            }).ToList();

            // Breakdown by sector
            var bySectorRaw = await _db.ExtractRows
                .AsNoTracking()
                .Where(r => r.BatchId == selectedBatchId)
                .GroupBy(r => (r.Sector ?? "Unknown").Trim())
                .Select(g => new
                {
                    Sector = g.Key,
                    Total = g.Count(),
                    Pending = g.Count(x => x.Decision == VerificationDecision.Pending),
                    Yes = g.Count(x => x.Decision == VerificationDecision.Yes),
                    No = g.Count(x => x.Decision == VerificationDecision.No)
                })
                .OrderByDescending(x => x.Pending)
                .ThenBy(x => x.Sector)
                .ToListAsync(ct);

            var bySector = bySectorRaw.Select(x =>
            {
                var sVerified = x.Yes + x.No;
                var sProgress = x.Total <= 0 ? 0m : Math.Round((decimal)sVerified * 100m / x.Total, 2);

                return new ManagerDashboardVm.SectorBreakdownVm
                {
                    Sector = x.Sector,
                    Total = x.Total,
                    Pending = x.Pending,
                    Yes = x.Yes,
                    No = x.No,
                    ProgressPercent = sProgress
                };
            }).ToList();

            return new ManagerDashboardVm
            {
                BatchId = batch.Id,
                BatchName = batch.BatchName,
                BatchDate = batch.BatchDate,
                WeekStart = batch.WeekWindow.WeekStart,
                WeekEnd = batch.WeekWindow.WeekEnd,
                NoticeSendDate = batch.WeekWindow.NoticeSendDate,

                Total = total,
                Pending = pending,
                Yes = yes,
                No = no,
                ProgressPercent = progress,

                // ✅ SLA fields
                SlaStatus = sla.Status,
                DaysToSend = sla.DaysToSend,
                SlaLabel = sla.Label,

                ManagersNotified = batch.ManagersNotifiedAtUtc != null,
                ManagersNotifiedAtUtc = batch.ManagersNotifiedAtUtc,

                Batches = batches,
                ByValuer = byValuer,
                BySector = bySector
            };
        }
    }
}
