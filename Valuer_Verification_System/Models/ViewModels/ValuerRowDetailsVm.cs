using Valuer_Verification_System.Models.Data;

namespace Valuer_Verification_System.Models.ViewModels
{
    public sealed class ValuerRowDetailsVm
    {
        public int ExtractRowId { get; set; }

        public int BatchId { get; set; }
        public string BatchName { get; set; } = "";
        public DateOnly BatchDate { get; set; }

        public DateOnly WeekStart { get; set; }
        public DateOnly WeekEnd { get; set; }
        public DateOnly NoticeSendDate { get; set; }

        // Property / extract fields (adjust names to your ExtractRow model)
        public string ValuationKey { get; set; } = "";
        public string? Sector { get; set; }
        public string? PropertyDescription { get; set; }
        public decimal? MarketValue { get; set; }
        public string? Category { get; set; }
        public string? PropertyType { get; set; }

        public string? ValuerFromExtract { get; set; }

        // Current assignment
        public string AssignedToValuer { get; set; } = "";

        // Decision
        public VerificationDecision Decision { get; set; }
        public DateTime? DecisionAtUtc { get; set; }
        public string? DecisionComment { get; set; }

        // History
        public List<AssignmentHistoryVm> AssignmentHistory { get; set; } = new();



        public sealed class AssignmentHistoryVm
        {
            public string FromValuer { get; set; } = "";
            public string ToValuer { get; set; } = "";
            public string ChangedBy { get; set; } = "";
            public DateTime ChangedAtUtc { get; set; }
            public string Reason { get; set; } = "";
        }
    }
}
