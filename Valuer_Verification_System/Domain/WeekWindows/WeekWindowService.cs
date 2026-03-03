using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Domain.Dates;
using Valuer_Verification_System.Models.Data;

namespace Valuer_Verification_System.Domain.WeekWindows
{
    public sealed class WeekWindowService : IWeekWindowService
    {
        private readonly VvsDbContext _db;
        private readonly IWeekWindowCalculator _calc;

        public WeekWindowService(VvsDbContext db, IWeekWindowCalculator calc)
        {
            _db = db;
            _calc = calc;
        }

        // ── Create or get ─────────────────────────────────────────────────────

        public async Task<WeekWindow> CreateOrGetAsync(DateOnly anyDateInWeek, string createdBy, CancellationToken ct)
        {
            var weekStart = _calc.SnapToWeekStart(anyDateInWeek);

            var existing = await _db.WeekWindows
                .FirstOrDefaultAsync(x => x.WeekStart == weekStart, ct);

            if (existing != null)
                return existing;

            var weekEnd = _calc.ComputeWeekEnd(weekStart);
            var sendDate = _calc.ComputeNoticeSendDate(weekEnd);
            var reviewOpen = _calc.ComputeReviewOpenDate(sendDate);
            var reviewClose = _calc.ComputeReviewCloseDate(reviewOpen);

            var ww = new WeekWindow
            {
                WeekStart = weekStart,
                WeekEnd = weekEnd,
                NoticeSendDate = sendDate,
                ReviewOpenDate = reviewOpen,
                ReviewCloseDate = reviewClose,
                Status = WeekWindowStatus.Draft,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow
            };

            _db.WeekWindows.Add(ww);

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Race condition: another request created the same week start
                var again = await _db.WeekWindows.FirstAsync(x => x.WeekStart == weekStart, ct);
                return again;
            }

            return ww;
        }

        // ── Lookup ────────────────────────────────────────────────────────────

        public Task<WeekWindow?> GetAsync(int id, CancellationToken ct)
            => _db.WeekWindows.FirstOrDefaultAsync(x => x.Id == id, ct);

        public async Task<WeekWindow?> GetByBatchAsync(int batchId, CancellationToken ct)
        {
            var batch = await _db.Batches
                .AsNoTracking()
                .Include(b => b.WeekWindow)
                .FirstOrDefaultAsync(b => b.Id == batchId, ct);

            return batch?.WeekWindow;
        }

        public async Task<List<WeekWindow>> ListAsync(int take = 200, CancellationToken ct = default)
        {
            return await _db.WeekWindows
                .OrderByDescending(x => x.WeekStart)
                .Take(take)
                .ToListAsync(ct);
        }

        // ── Status transition ─────────────────────────────────────────────────

        public async Task UpdateStatusAsync(int id, WeekWindowStatus newStatus, string updatedBy, CancellationToken ct)
        {
            var ww = await _db.WeekWindows.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (ww is null)
                throw new InvalidOperationException($"WeekWindow {id} not found.");

            // Basic guard: prevent moving backwards
            if ((int)newStatus < (int)ww.Status)
                throw new InvalidOperationException(
                    $"Cannot move WeekWindow from {ww.Status} back to {newStatus}.");

            ww.Status = newStatus;
            ww.UpdatedBy = updatedBy;
            ww.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
        }

        // ── Existence check ───────────────────────────────────────────────────

        public Task<bool> ExistsForWeekStartAsync(DateOnly weekStart, CancellationToken ct)
            => _db.WeekWindows.AnyAsync(x => x.WeekStart == weekStart, ct);
    }
}