using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Models.Data;
using Valuer_Verification_System.Models.SLA;
using Valuer_Verification_System.Models.ViewModels;


namespace Valuer_Verification_System.Services.Manager
{
    public interface IManagerDashboardService
    {
        Task<ManagerDashboardVm> BuildAsync(int? batchId, string? sector, CancellationToken ct);
    }

    public sealed class ManagerDashboardService : IManagerDashboardService
    {
        private readonly VvsDbContext _db;
        private readonly IManagerWeekViewService _week;

        public ManagerDashboardService(VvsDbContext db, IManagerWeekViewService week)
        {
            _db = db;
            _week = week;
        }

        public async Task<ManagerDashboardVm> BuildAsync(int? batchId, string? sector, CancellationToken ct)
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

            // Default: the newest batch the Project Manager approved (a working week table), else the newest batch.
            var defaultBatchId = await _db.Batches
                .AsNoTracking()
                .Where(b => b.CleanWeekTableName != null && b.CleanWeekTableName != ""
                         && b.ProjectManagerApprovedAtUtc != null)
                .OrderByDescending(b => b.Id)
                .Select(b => (int?)b.Id)
                .FirstOrDefaultAsync(ct);

            var selectedBatchId = batchId ?? defaultBatchId ?? batches.FirstOrDefault()?.Id ?? 0;

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

            // Source extract file used by new folder-import flow
            var extractFile = await _db.ExtractFiles
                .AsNoTracking()
                .Where(x => x.BatchId == selectedBatchId)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync(ct);

            // Overall counts from ExtractRows
            var rowsQ = _db.ExtractRows
                .AsNoTracking()
                .Where(r => r.BatchId == selectedBatchId);

            var total = await rowsQ.CountAsync(ct);

            var pending = await rowsQ.CountAsync(
                r => r.Decision == VerificationDecision.Pending,
                ct);

            var yes = await rowsQ.CountAsync(
                r => r.Decision == VerificationDecision.Yes,
                ct);

            var no = await rowsQ.CountAsync(
                r => r.Decision == VerificationDecision.No,
                ct);

            var verified = yes + no;

            var progress = total <= 0
                ? 0m
                : Math.Round((decimal)verified * 100m / total, 2);

            // SLA
            var sla = SlaCalculator.Evaluate(batch.WeekWindow.NoticeSendDate, pending);

            // Breakdown by valuer.
            // New source of truth is ExtractRows.Username.
            var byValuerRaw = await rowsQ
                .GroupBy(r => string.IsNullOrWhiteSpace(r.Username)
                    ? "Unassigned"
                    : r.Username!.Trim())
                .Select(g => new
                {
                    Valuer = g.Key,
                    Total = g.Count(),
                    Pending = g.Count(x => x.Decision == VerificationDecision.Pending),
                    Yes = g.Count(x => x.Decision == VerificationDecision.Yes),
                    No = g.Count(x => x.Decision == VerificationDecision.No)
                })
                .OrderByDescending(x => x.Pending)
                .ThenBy(x => x.Valuer)
                .ToListAsync(ct);

            var byValuer = byValuerRaw.Select(x =>
            {
                var vVerified = x.Yes + x.No;

                var vProgress = x.Total <= 0
                    ? 0m
                    : Math.Round((decimal)vVerified * 100m / x.Total, 2);

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

            // Breakdown by sector from ExtractRows
            var bySectorRaw = await rowsQ
                .GroupBy(r => string.IsNullOrWhiteSpace(r.Sector)
                    ? "Unassigned"
                    : r.Sector!.Trim())
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

                var sProgress = x.Total <= 0
                    ? 0m
                    : Math.Round((decimal)sVerified * 100m / x.Total, 2);

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

            // The week table is the working table: when it exists, every number comes from it.
            var sectorFilter = string.IsNullOrWhiteSpace(sector) || sector == "*" ? null : sector.Trim();
            var properties = 0;

            ManagerWeekView? wv = null;
            if (!string.IsNullOrWhiteSpace(batch.CleanWeekTableName))
            {
                try
                {
                    wv = await _week.GetAsync(batch.CleanWeekTableName!, sectorFilter, ct);
                }
                catch (Microsoft.Data.SqlClient.SqlException)
                {
                    // An older week table without the working columns: keep the ExtractRows numbers.
                }
            }

            if (wv != null)
            {

                total = wv.Total;
                pending = wv.Pending;
                yes = wv.Yes;
                no = wv.No;
                properties = wv.Properties;
                progress = total <= 0 ? 0m : Math.Round((decimal)(yes + no) * 100m / total, 2);
                sla = SlaCalculator.Evaluate(batch.WeekWindow.NoticeSendDate, pending);

                byValuer = wv.Valuers
                    .Select(x => new ManagerDashboardVm.ValuerBreakdownVm
                    {
                        Valuer = x.Valuer,
                        Total = x.Total,
                        Pending = x.Pending,
                        Yes = x.Yes,
                        No = x.No,
                        ProgressPercent = x.Total <= 0 ? 0m : Math.Round((decimal)(x.Yes + x.No) * 100m / x.Total, 2)
                    })
                    .OrderByDescending(x => x.Pending)
                    .ThenBy(x => x.Valuer)
                    .ToList();

                bySector = wv.Sectors
                    .Select(x => new ManagerDashboardVm.SectorBreakdownVm
                    {
                        Sector = x.Sector,
                        Total = x.Total,
                        Pending = x.Pending,
                        Yes = x.Yes,
                        No = x.No,
                        ProgressPercent = x.Total <= 0 ? 0m : Math.Round((decimal)(x.Yes + x.No) * 100m / x.Total, 2)
                    })
                    .OrderByDescending(x => x.Pending)
                    .ThenBy(x => x.Sector)
                    .ToList();
            }

            return new ManagerDashboardVm
            {
                BatchId = batch.Id,
                Sector = sectorFilter,
                Properties = properties,
                BatchName = batch.BatchName,
                BatchDate = batch.BatchDate,
                Status = batch.Status.ToString(),

                WeekStart = batch.WeekWindow.WeekStart,
                WeekEnd = batch.WeekWindow.WeekEnd,
                NoticeSendDate = batch.WeekWindow.NoticeSendDate,

                Total = total,
                Pending = pending,
                Yes = yes,
                No = no,
                ProgressPercent = progress,

                // SLA fields
                SlaStatus = sla.Status,
                DaysToSend = sla.DaysToSend,
                SlaLabel = sla.Label,

                // New folder-import file details
                OriginalFileName = extractFile?.OriginalFileName,
                SourceFilePath = extractFile?.SavedPath,
                UploadedAtUtc = extractFile?.UploadedAtUtc,

                // Cleaning status
                ExtractCleaningStatus = batch.ExtractCleaningStatus,
                ExtractCleanedAtUtc = batch.ExtractCleanedAtUtc,
                ExtractCleanedBy = batch.ExtractCleanedBy,
                CleanWeekTableName = batch.CleanWeekTableName,
                ExtractCleaningMessage = batch.ExtractCleaningMessage,

                // QA + Project Manager notification
                ProjectManagerNotifiedAtUtc = batch.ProjectManagerNotifiedAtUtc,
                ProjectManagerNotifiedTo = batch.ProjectManagerNotifiedTo,
                QaNotifiedTo = batch.QaNotifiedTo,

                // Project Manager approval
                ProjectManagerApprovedAtUtc = batch.ProjectManagerApprovedAtUtc,
                ProjectManagerApprovedBy = batch.ProjectManagerApprovedBy,
                ProjectManagerApprovalComment = batch.ProjectManagerApprovalComment,

                // Sector Manager notification
                ManagersNotified = batch.ManagersNotifiedAtUtc != null,
                ManagersNotifiedAtUtc = batch.ManagersNotifiedAtUtc,

                Batches = batches,
                ByValuer = byValuer,
                BySector = bySector
            };
        }
    }
}