namespace Valuer_Verification_System.Models.ViewModels
{
    using Valuer_Verification_System.Models.Data;

    public sealed class Section78StatsVm
    {
        public string FinancialYear { get; set; } = "2025_26";
        public List<string> FinancialYears { get; set; } = new() { "2024_25", "2025_26" };

        // Detailed week-level rows (for editable grid)
        public List<Section78StatEntry> Rows { get; set; } = new();

        // Aggregated monthly view (matches email screenshot)
        public List<MonthlySummaryRow> MonthlySummary { get; set; } = new();

        // Available batches for auto-populate
        public List<BatchOption> AvailableBatches { get; set; } = new();

        public sealed class MonthlySummaryRow
        {
            public string Period { get; set; } = "";
            public string OpenYN { get; set; } = "Y";
            public int TotalEntries { get; set; }
            public DateOnly? ReviewPeriodOpen { get; set; }
            public DateOnly? ReviewPeriodClosed { get; set; }
            public int ReviewsReceived { get; set; }
            public string? Closed { get; set; }

            public string? WeekTableName { get; set; }
        }

        public sealed class BatchOption
        {
            public int Id { get; set; }
            public string BatchName { get; set; } = "";
            public string WeekTable { get; set; } = "";
            public DateOnly BatchDate { get; set; }
            public bool HasStats { get; set; }
        }
    }
}