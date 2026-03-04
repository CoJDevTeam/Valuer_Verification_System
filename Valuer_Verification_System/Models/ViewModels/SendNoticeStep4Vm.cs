namespace Valuer_Verification_System.Models.ViewModels
{
    /// <summary>
    /// Step 4 – Confirmation page: review counts and dates, then confirm send.
    /// </summary>
    public sealed class SendNoticeStep4Vm
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = "";
        public string WeekTableName { get; set; } = "";

        // Dates (from WeekWindow — never recalculated)
        public DateOnly NoticeSendDate { get; set; }
        public DateOnly ReviewOpenDate { get; set; }
        public DateOnly ReviewCloseDate { get; set; }

        // Row counts
        public int TotalRows { get; set; }   // all rows in batch
        public int NoticeSendCount { get; set; }   // SendNotice = 'Y' AND Decision = Yes

        // Already-sent guard (prevents double-send)
        public bool AlreadySent { get; set; }
        public DateTime? SentAtUtc { get; set; }
        public string? SentBy { get; set; }
    }
}