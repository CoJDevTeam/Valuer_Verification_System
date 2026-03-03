namespace Valuer_Verification_System.Domain.Dates
{
    public interface IWeekWindowCalculator
    {
        DateOnly SnapToWeekStart(DateOnly anyDate);       // Monday
        DateOnly ComputeWeekEnd(DateOnly weekStart);      // Sunday
        DateOnly ComputeNoticeSendDate(DateOnly weekEnd); // Next Friday or Thu if holiday

        /// <summary>Last Friday of the same month as noticeSendDate. If holiday → last Thursday.</summary>
        DateOnly ComputeReviewOpenDate(DateOnly noticeSendDate);

        /// <summary>ReviewOpenDate + 60 calendar days.</summary>
        DateOnly ComputeReviewCloseDate(DateOnly reviewOpenDate);
    }
}
