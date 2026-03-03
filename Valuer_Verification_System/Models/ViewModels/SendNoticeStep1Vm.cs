namespace Valuer_Verification_System.Models.ViewModels
{// ── Step 1 – Week & Batch Summary ────────────────────────────────────────
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
        public DateOnly ExtractionDate { get; set; }
        public DateOnly NoticeSendDate { get; set; }

        // Data Team inputs
        public string LisMasterTable { get; set; } = "LIS_MASTER.dbo.LIS_2025_09_16";
        public string SapTable { get; set; } = "SAP_STATEMENTS_RECORDS.dbo.SAP_STATEMENT_RECORDS_2026_01";

        // Auto-generated table name on 173 server
        public string WeekTableName { get; set; } = "";   // GV23_LIS_SV_YYYY_MM_EXTRACT_Wn

        public bool CleaningDone { get; set; }
        public string? ErrorMessage { get; set; }
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
