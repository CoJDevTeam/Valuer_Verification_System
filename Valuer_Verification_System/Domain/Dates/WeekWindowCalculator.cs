using Microsoft.Extensions.Options;
using Valuer_Verification_System.Infrastructure.Configuration;

namespace Valuer_Verification_System.Domain.Dates
{
    public sealed class WeekWindowCalculator : IWeekWindowCalculator
    {
        private readonly HashSet<DateOnly> _holidays;

        public WeekWindowCalculator(IOptions<HolidaySettings> opt)
        {
            _holidays = opt.Value.Dates.ToHashSet();
        }

        // ── Existing ─────────────────────────────────────────────────────────

        public DateOnly SnapToWeekStart(DateOnly anyDate)
        {
            var dow = anyDate.DayOfWeek;
            var diff = dow == DayOfWeek.Sunday ? 6 : ((int)dow - (int)DayOfWeek.Monday);
            return anyDate.AddDays(-diff);
        }

        public DateOnly ComputeWeekEnd(DateOnly weekStart) => weekStart.AddDays(6);

        /// <summary>weekEnd is Sunday → next Friday = +5 days. If holiday, take Thursday.</summary>
        public DateOnly ComputeNoticeSendDate(DateOnly weekEnd)
        {
            var nextFriday = weekEnd.AddDays(5);
            return _holidays.Contains(nextFriday) ? nextFriday.AddDays(-1) : nextFriday;
        }

        // ── New ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Last Friday of the SAME calendar month as <paramref name="noticeSendDate"/>.
        /// If that Friday is a public holiday → last Thursday of the month.
        /// </summary>
        public DateOnly ComputeReviewOpenDate(DateOnly noticeSendDate)
        {
            // Walk backwards from last day of month until we hit a Friday
            var lastDay = new DateOnly(noticeSendDate.Year, noticeSendDate.Month,
                                       DateTime.DaysInMonth(noticeSendDate.Year, noticeSendDate.Month));

            var candidate = lastDay;
            while (candidate.DayOfWeek != DayOfWeek.Friday)
                candidate = candidate.AddDays(-1);

            // Holiday check: if last Friday is a holiday, use last Thursday
            if (_holidays.Contains(candidate))
                candidate = candidate.AddDays(-1); // Thursday

            return candidate;
        }

        /// <summary>ReviewOpenDate + 60 calendar days.</summary>
        public DateOnly ComputeReviewCloseDate(DateOnly reviewOpenDate)
            => reviewOpenDate.AddDays(60);
    }
}