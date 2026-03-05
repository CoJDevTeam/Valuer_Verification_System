namespace Valuer_Verification_System.Models.ViewModels
{
    public sealed class SendNoticeStep4Vm
    {
        // ── Batch selector (mirrors PrintNoticeVm pattern) ────────────────────
        public List<BatchOption> AvailableBatches { get; set; } = new();
        public int SelectedBatchId { get; set; }

        // ── Selected batch details ─────────────────────────────────────────────
        public int BatchId { get; set; }
        public string BatchName { get; set; } = "";
        public string WeekTableName { get; set; } = "";

        // ── Dates ──────────────────────────────────────────────────────────────
        public DateOnly NoticeSendDate { get; set; }
        public DateOnly ReviewOpenDate { get; set; }
        public DateOnly ReviewCloseDate { get; set; }

        // ── Counts ─────────────────────────────────────────────────────────────
        public int TotalNotices { get; set; }
        public int NoSendCount { get; set; }   // SendNotice != Y
        public int TotalRows { get; set; }

        // ── Folder paths ───────────────────────────────────────────────────────
        public string PrintFolderDisplay { get; set; } = "";
        public string EmailFolderDisplay { get; set; } = "";

        // ── Email preview HTML (from EmailTemplates.ClientNotice) ─────────────
        public string EmailPreviewHtml { get; set; } = "";

        // ── Already-sent guard ─────────────────────────────────────────────────
        public bool AlreadySent { get; set; }
        public DateTime? SentAtUtc { get; set; }
        public string? SentBy { get; set; }

        // ── Batch option (for dropdown) ────────────────────────────────────────
        public sealed class BatchOption
        {
            public int Id { get; set; }
            public string BatchName { get; set; } = "";
            public string WeekTableName { get; set; } = "";
            public DateOnly NoticeSendDate { get; set; }
            public int NoticeCount { get; set; }
        }
    }
}