namespace Valuer_Verification_System.Models.ViewModels
{

    public sealed class ValuerDashboardVm
    {
        public string ValuerName { get; set; } = "";

        // Overall (all batches)
        public int TotalAssigned { get; set; }
        public int Pending { get; set; }
        public int Yes { get; set; }
        public int No { get; set; }

        public decimal ProgressPercent { get; set; } // (Yes+No)/Total * 100

        // Today performance (UTC or local - we’ll do UTC now to match DB)
        public int VerifiedToday { get; set; }
        public int YesToday { get; set; }
        public int NoToday { get; set; }

        // Batches summary (only batches this valuer is assigned to)
        public List<BatchSummaryVm> MyBatches { get; set; } = new();

        public sealed class BatchSummaryVm
        {
            public int BatchId { get; set; }
            public string BatchName { get; set; } = "";
            public DateOnly BatchDate { get; set; }
            public DateOnly WeekStart { get; set; }
            public DateOnly WeekEnd { get; set; }
            public DateOnly NoticeSendDate { get; set; }

            public int Total { get; set; }
            public int Pending { get; set; }
            public int Yes { get; set; }
            public int No { get; set; }
            public decimal ProgressPercent { get; set; }
        }
    }
}