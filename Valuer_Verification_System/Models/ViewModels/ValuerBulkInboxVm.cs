using Valuer_Verification_System.Models.Data;

namespace Valuer_Verification_System.Models.ViewModels
{
    public sealed class ValuerBulkInboxVm
    {
        public int BatchId { get; set; }

        public string ValuerUsername { get; set; } = "";

        public List<ValuerBulkGroupVm> Groups { get; set; } = new();
    }

    public sealed class ValuerBulkGroupVm
    {
        public int BatchId { get; set; }

        public string PremiseId { get; set; } = "";

        public string UnitKey { get; set; } = "";

        public string PropertyDescription { get; set; } = "";

        public string TownNameDesc { get; set; } = "";

        public string UnitType { get; set; } = "";

        public string UnitNo { get; set; } = "";

        public int RowCount { get; set; }

        public int PendingCount { get; set; }

        public int YesCount { get; set; }

        public int NoCount { get; set; }

        public bool IsSafeForBulk { get; set; }

        public string SafetyMessage { get; set; } = "";
    }

    public sealed class ValuerBulkDetailsVm
    {
        public int BatchId { get; set; }

        public string PremiseId { get; set; } = "";

        public string UnitKey { get; set; } = "";

        public string PropertyDescription { get; set; } = "";

        public string TownNameDesc { get; set; } = "";

        public string UnitType { get; set; } = "";

        public string UnitNo { get; set; } = "";

        public bool IsSafeForBulk { get; set; }

        public string SafetyMessage { get; set; } = "";

        public List<ValuerBulkRowVm> Rows { get; set; } = new();
    }

    public sealed class ValuerBulkRowVm
    {
        public int ExtractRowId { get; set; }

        public string ValuationKey { get; set; } = "";

        public string ValuationKey2 { get; set; } = "";

        public string PropertyDescription { get; set; } = "";

        public string Category { get; set; } = "";

        public decimal? MarketValue { get; set; }

        public decimal? RateableArea { get; set; }

        public string LegalArea { get; set; } = "";

        public string RightDescription { get; set; } = "";

        public string RightsStatus { get; set; } = "";

        public DateOnly? WefDate { get; set; }

        public VerificationDecision Decision { get; set; }

        public string DecisionText => Decision.ToString();
    }

    public sealed class BulkVerifyRequestVm
    {
        public int BatchId { get; set; }

        public string PremiseId { get; set; } = "";

        public string UnitKey { get; set; } = "";

        public string? Comment { get; set; }
    }
}
