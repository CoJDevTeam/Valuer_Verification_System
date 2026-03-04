namespace Valuer_Verification_System.Models.ViewModels
{
    public sealed class PrintNoticeVm
    {
        // Batch selector
        public List<BatchOption> AvailableBatches { get; set; } = new();
        public int SelectedBatchId { get; set; }

        // Batch details
        public string BatchName { get; set; } = "";
        public string WeekTableName { get; set; } = "";
        public DateOnly ExtractionDate { get; set; }
        public DateOnly NoticeSendDate { get; set; }
        public DateOnly ReviewOpenDate { get; set; }
        public DateOnly ReviewCloseDate { get; set; }

        // Counts  (each "notice" = one ValuationKey group = one PDF)
        public int TotalNotices { get; set; }  // distinct ValuationKey groups to print
        public int SingleCount { get; set; }  // groups where CatDesc != Multipurpose
        public int MultiPurposeCount { get; set; }  // groups where CatDesc = Multipurpose

        // File-system output folder shown to the user
        public string OutputFolderDisplay { get; set; } = "";

        // ── Nested ────────────────────────────────────────────────────────
        public sealed class BatchOption
        {
            public int Id { get; set; }
            public string BatchName { get; set; } = "";
            public string WeekTableName { get; set; } = "";
            public DateOnly NoticeSendDate { get; set; }
            public int NoticeCount { get; set; }
        }
    }

    // ── AJAX request / response models ───────────────────────────────────────
    public sealed class PrintChunkRequest
    {
        public int BatchId { get; set; }
        public string WeekTableName { get; set; } = "";
        public int Offset { get; set; }
        public int ChunkSize { get; set; } = 50;
    }

    public sealed class PrintChunkResult
    {
        public bool Success { get; set; }
        public int Printed { get; set; }
        public int Total { get; set; }
        public bool Done { get; set; }
        public string? CurrentProperty { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}