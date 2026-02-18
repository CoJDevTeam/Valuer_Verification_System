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

        public DateOnly SnapToWeekStart(DateOnly anyDate)
        {
            // Monday is week start. If Sunday, go back 6 days.
            var dow = anyDate.DayOfWeek;
            var diff = dow == DayOfWeek.Sunday ? 6 : ((int)dow - (int)DayOfWeek.Monday);
            return anyDate.AddDays(-diff);
        }

        public DateOnly ComputeWeekEnd(DateOnly weekStart)
            => weekStart.AddDays(6);

        public DateOnly ComputeNoticeSendDate(DateOnly weekEnd)
        {
            // weekEnd is Sunday -> next Friday = +5 days
            var nextFriday = weekEnd.AddDays(5);

            // Holiday rule: if Friday is a public holiday, send on Thursday
            if (_holidays.Contains(nextFriday))
                return nextFriday.AddDays(-1);

            return nextFriday;
        }
    }

}
