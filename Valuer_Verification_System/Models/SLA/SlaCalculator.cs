namespace Valuer_Verification_System.Models.SLA
{
    public static class SlaCalculator
    {
        // Johannesburg timezone requirement
        public static DateOnly GetTodayZa()
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");
            var nowZa = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);
            return DateOnly.FromDateTime(nowZa.Date);
        }

        public static SlaResult Evaluate(DateOnly noticeSendDate, int pending)
        {
            var today = GetTodayZa();
            var daysToSend = noticeSendDate.DayNumber - today.DayNumber;

            if (pending <= 0)
            {
                return new SlaResult
                {
                    Status = SlaStatus.Completed,
                    DaysToSend = daysToSend,
                    Label = "Completed"
                };
            }

            // Pending > 0
            if (daysToSend < 0)
            {
                return new SlaResult
                {
                    Status = SlaStatus.Overdue,
                    DaysToSend = daysToSend,
                    Label = $"Overdue by {Math.Abs(daysToSend)} day(s)"
                };
            }

            // You can tune these thresholds anytime:
            // - AtRisk: send date is today or tomorrow and still pending
            // - OnTrack: 2+ days left
            if (daysToSend <= 1)
            {
                return new SlaResult
                {
                    Status = SlaStatus.AtRisk,
                    DaysToSend = daysToSend,
                    Label = daysToSend == 0 ? "Send is today (pending!)" : "Send is tomorrow (pending!)"
                };
            }

            return new SlaResult
            {
                Status = SlaStatus.OnTrack,
                DaysToSend = daysToSend,
                Label = $"{daysToSend} day(s) to send"
            };
        }
    }
}
