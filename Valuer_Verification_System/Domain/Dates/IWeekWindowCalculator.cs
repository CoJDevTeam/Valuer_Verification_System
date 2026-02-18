namespace Valuer_Verification_System.Domain.Dates
{
    public interface IWeekWindowCalculator
    {
        DateOnly SnapToWeekStart(DateOnly anyDate);       // Monday
        DateOnly ComputeWeekEnd(DateOnly weekStart);      // Sunday
        DateOnly ComputeNoticeSendDate(DateOnly weekEnd); // Next Friday or Thu if holiday
    }
}
