using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Models.ViewModels;

namespace Valuer_Verification_System.Services.Valuer
{
    public interface IValuerRowDetailsService
    {
        Task<ValuerRowDetailsVm> BuildAsync(int extractRowId, string valuer, CancellationToken ct);
    }

    public sealed class ValuerRowDetailsService : IValuerRowDetailsService
    {
        private readonly VvsDbContext _db;

        public ValuerRowDetailsService(VvsDbContext db)
        {
            _db = db;
        }

        public async Task<ValuerRowDetailsVm> BuildAsync(int extractRowId, string valuer, CancellationToken ct)
        {
            if (extractRowId <= 0)
                throw new InvalidOperationException("Invalid row id.");

            if (string.IsNullOrWhiteSpace(valuer))
                valuer = "UNKNOWN_VALUER";

            // Security: ensure this valuer is assigned to this row
            var assignment = await _db.Assignments
                .AsNoTracking()
                .Include(a => a.ExtractRow)
                .FirstOrDefaultAsync(a => a.ExtractRowId == extractRowId && a.AssignedToValuer == valuer, ct);

            if (assignment is null)
                throw new InvalidOperationException("You are not allowed to view this record (not assigned to you).");

            var row = assignment.ExtractRow;

            // Load batch + week info
            var batch = await _db.Batches
                .AsNoTracking()
                .Include(b => b.WeekWindow)
                .FirstOrDefaultAsync(b => b.Id == row.BatchId, ct);

            if (batch is null)
                throw new InvalidOperationException("Batch not found.");

            // Assignment history for this assignment (audit)
            // If you store AssignmentHistory by AssignmentId, this is correct.
            var hist = await _db.AssignmentHistories
                .AsNoTracking()
                .Where(h => h.AssignmentId == assignment.Id)
                .OrderByDescending(h => h.ChangedAtUtc)
                .Select(h => new ValuerRowDetailsVm.AssignmentHistoryVm
                {
                    FromValuer = h.FromValuer,
                    ToValuer = h.ToValuer,
                    ChangedBy = h.ChangedBy,
                    ChangedAtUtc = h.ChangedAtUtc,
                    Reason = h.Reason
                })
                .ToListAsync(ct);

            return new ValuerRowDetailsVm
            {
                ExtractRowId = row.Id,

                BatchId = batch.Id,
                BatchName = batch.BatchName,
                BatchDate = batch.BatchDate,

                WeekStart = batch.WeekWindow.WeekStart,
                WeekEnd = batch.WeekWindow.WeekEnd,
                NoticeSendDate = batch.WeekWindow.NoticeSendDate,

                // Map fields (adjust names if your ExtractRow uses different property names)
                ValuationKey = row.ValuationKey,
                Sector = row.Sector,
                PropertyDescription = row.PropertyDescription,
                MarketValue = row.MarketValue,
                Category = row.Category,
                PropertyType = row.PropertyType,
                ValuerFromExtract = row.Valuer,

                AssignedToValuer = assignment.AssignedToValuer,

                Decision = row.Decision,
                DecisionAtUtc = row.DecisionAtUtc,
                DecisionComment = row.DecisionComment,

                AssignmentHistory = hist
            };
        }
    }
}
