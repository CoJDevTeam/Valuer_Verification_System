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

        public async Task<WeekWindow> CreateOrGetAsync(DateOnly anyDateInWeek, string createdBy, CancellationToken ct)
        {
            var weekStart = _calc.SnapToWeekStart(anyDateInWeek);

            var existing = await _db.WeekWindows
                .FirstOrDefaultAsync(x => x.WeekStart == weekStart, ct);

            if (existing != null)
                return existing;

            var weekEnd = _calc.ComputeWeekEnd(weekStart);
            var sendDate = _calc.ComputeNoticeSendDate(weekEnd);

            var ww = new WeekWindow
            {
                WeekStart = weekStart,
                WeekEnd = weekEnd,
                NoticeSendDate = sendDate,
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
                // In case of race/duplicate WeekStart unique index
                var again = await _db.WeekWindows.FirstAsync(x => x.WeekStart == weekStart, ct);
                return again;
            }

            return ww;
        }

        public Task<WeekWindow?> GetAsync(int id, CancellationToken ct)
            => _db.WeekWindows.FirstOrDefaultAsync(x => x.Id == id, ct);

        public async Task<List<WeekWindow>> ListAsync(int take = 200, CancellationToken ct = default)
        {
            return await _db.WeekWindows
                .OrderByDescending(x => x.WeekStart)
                .Take(take)
                .ToListAsync(ct);
        }
    }
}
