namespace Valuer_Verification_System.Models.ViewModels
{
    // ── Step 1 – Week & Batch Summary ────────────────────────────────────────
    public sealed class SendNoticeStep1Vm
    {
        // Available weeks for dropdown
        public List<WeekOptionVm> Weeks { get; set; } = new();

        // After selection
        public int? SelectedWeekWindowId { get; set; }
        public int? SelectedBatchId { get; set; }

        // Summary panel (populated after selection)
        public BatchSummaryVm? Summary { get; set; }

        public sealed class WeekOptionVm
        {
            public int Id { get; set; }
            public DateOnly WeekStart { get; set; }
            public DateOnly WeekEnd { get; set; }
            public DateOnly NoticeSendDate { get; set; }
            public string Label => $"{WeekStart:dd MMM yyyy} – {WeekEnd:dd MMM yyyy}  (Send: {NoticeSendDate:dd MMM yyyy})";
        }

        public sealed class BatchSummaryVm
        {
            public int BatchId { get; set; }
            public string BatchName { get; set; } = "";
            public DateOnly BatchDate { get; set; }
            public DateOnly ExtractionDate { get; set; }
            public DateOnly NoticeSendDate { get; set; }   // = LetterDate
            public DateOnly ReviewOpenDate { get; set; }
            public DateOnly ReviewCloseDate { get; set; }
            public int Total { get; set; }
            public int Verified { get; set; }   // Decision = Yes OR No
            public int Pending { get; set; }
            public bool AllVerified => Pending == 0 && Total > 0;
        }
    }

    // ── Step 2 – Cleaning Configuration & Progress ───────────────────────────
    public sealed class SendNoticeStep2Vm
    {
        public int BatchId { get; set; }
        public int WeekWindowId { get; set; }

        public string BatchName { get; set; } = "";
        public DateOnly ExtractionDate { get; set; }
        public DateOnly NoticeSendDate { get; set; }
        public string WeekTableName { get; set; } = "";

        public DateTime? ExtractCleanedAtUtc { get; set; }
        public string? ExtractCleaningStatus { get; set; }
        public string? ExtractCleaningMessage { get; set; }

        public int TotalRows { get; set; }
        public int Pending { get; set; }
        public int Yes { get; set; }
        public int No { get; set; }

        public int MissingSector { get; set; }
        public int MissingPremiseId { get; set; }
        public int MissingUsername { get; set; }
        public int SendNoticeYes { get; set; }

        public bool WeekTableExists { get; set; }

        public bool ReadyToInsert =>
            TotalRows > 0 &&
            Pending == 0 &&
            MissingSector == 0 &&
            MissingPremiseId == 0 &&
            MissingUsername == 0;
    }

    // ── Cleaning step result (returned by AJAX) ──────────────────────────────
    public sealed class CleaningStepResultVm
    {
        public bool Success { get; set; }
        public string StepName { get; set; } = "";
        public int RowsAffected { get; set; }
        public string? Error { get; set; }
    }
}