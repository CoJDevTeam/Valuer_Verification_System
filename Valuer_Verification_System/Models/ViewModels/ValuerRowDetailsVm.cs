namespace Valuer_Verification_System.Models.ViewModels
{
    using Valuer_Verification_System.Models.Data;

    /// <summary>
    /// All data shown in the Details modal when a valuer clicks 🔍 View.
    /// Every field comes directly from the ExtractRows table.
    /// Split rows (same ValuationKey, SplitInd P/Y) are included in SplitRows.
    /// </summary>
    public sealed class ValuerRowDetailsVm
    {
        // ── Identity ──────────────────────────────────────────────────────────
        public int ExtractRowId { get; set; }
        public string ValuationKey { get; set; } = "";
        public int BatchId { get; set; }
        public string BatchName { get; set; } = "";

        // ── Property identification ───────────────────────────────────────────
        public string? PropertyDesc { get; set; }   // PROPERTY_DESC (cleaned)
        public string? PropertyDescription { get; set; }   // PROPERTY_DESCRIPTION (raw)
        public string? TownNameDesc { get; set; }   // TOWN_NAME_DESC
        public string? StandNo { get; set; }   // STAND_NO
        public string? PremiseId { get; set; }   // PREMISE_ID
        public string? SgId { get; set; }   // SG_ID
        public string? PropertyId { get; set; }   // PROPERTY_ID

        // ── Erf / Ptn / Re ───────────────────────────────────────────────────
        public string? Erf { get; set; }
        public string? Ptn { get; set; }
        public string? Re { get; set; }

        // ── Category / type ───────────────────────────────────────────────────
        public string? CatCode { get; set; }   // CAT_CODE
        public string? CatDesc { get; set; }   // CAT_DESC
        public string? PropertyCategory { get; set; }   // PROPERTY_CATEGORY (alias)
        public string? StatusSubtype { get; set; }   // STATUS_SUBTYPE
        public string? StatusCode { get; set; }   // STATUS_CODE
        public string? PropertyType { get; set; }

        public string? AssignedToValuer { get; set; }
        // ── Valuation figures ─────────────────────────────────────────────────
        public decimal? MarketValue { get; set; }   // MARKET_VALUE
        public decimal? RateableArea { get; set; }   // RATEABLE_AREA
        public string? LegalArea { get; set; }   // LEGAL_AREA  (string in LIS)
        public string? Extent { get; set; }   // EXTENT (cleaned)
        public DateOnly? ValuationDate { get; set; }   // VALUATION_DATE
        public DateOnly? WefDate { get; set; }   // WEF_DATE
        public string? ValuationType { get; set; }   // VALUATION_TYPE
        public string? ValuationRollKey { get; set; }  // VALUATION_ROLL_KEY

        // ── Split info ────────────────────────────────────────────────────────
        public string? ValuationSplitNo { get; set; }  // VALUATION_SPLIT_NO
        public string? ValuationSplitInd { get; set; }  // VALUATION_SPLIT_IND (P/Y/null)
        public bool IsMultipurpose { get; set; }  // true when siblings exist

        // ── Unit / rights ─────────────────────────────────────────────────────
        public string? UnitKey { get; set; }
        public string? UnitType { get; set; }
        public string? UnitNo { get; set; }
        public string? UnitRightsKey { get; set; }
        public string? RightsPremise { get; set; }
        public decimal? RightsPremiseSize { get; set; }
        public string? RightsStatus { get; set; }
        public string? RightDescription { get; set; }

        // ── Address / contact ─────────────────────────────────────────────────
        public string? OwnerName { get; set; }
        public string? Addr1 { get; set; }
        public string? Addr2 { get; set; }
        public string? Addr3 { get; set; }
        public string? Addr4 { get; set; }
        public string? Addr5 { get; set; }
        public string? PhysicalAddress { get; set; }
        public string? PremiseAddress { get; set; }
        public string? EmailAddr { get; set; }
        public string? EmailInd { get; set; }

        // ── Flags ─────────────────────────────────────────────────────────────
        public string? SendNotice { get; set; }
        public string? CojOwned { get; set; }
        public string? Usable { get; set; }
        public string? SapData { get; set; }
        public string? RemarkDesc { get; set; }
        public string? Remarks { get; set; }

        // ── Dates ─────────────────────────────────────────────────────────────
        public DateOnly? ExtractionDate { get; set; }
        public DateOnly? ReviewOpenDate { get; set; }
        public DateOnly? ReviewCloseDate { get; set; }
        public string? SvCapturePeriod { get; set; }

        // ── Assignment & Valuer ──────────────────────────────────────────────
        public string? Valuer { get; set; }   // from ExtractRows.Valuer (LIS VALUER column)
        public string? Sector { get; set; }   // from ExtractRows.Sector

        // ── Workflow ──────────────────────────────────────────────────────────
        public VerificationDecision Decision { get; set; }
        public string? DecisionComment { get; set; }
        public DateTime? DecisionAtUtc { get; set; }
        public string? AssignedTo { get; set; }

        public DateOnly BatchDate { get; set; }

        public DateOnly WeekStart { get; set; }

        public DateOnly WeekEnd { get; set; }

        public DateOnly NoticeSendDate { get; set; }

        // ── Split siblings (other rows with same ValuationKey in same batch) ──
        public List<SplitRowVm> SplitRows { get; set; } = new();

        public sealed class SplitRowVm
        {
            public int ExtractRowId { get; set; }
            public string? ValuationSplitNo { get; set; }
            public string? ValuationSplitInd { get; set; }  // P = parent, Y = split
            public string? CatCode { get; set; }
            public string? CatDesc { get; set; }
            public decimal? MarketValue { get; set; }
            public string? LegalArea { get; set; }
            public string? Extent { get; set; }
            public DateOnly? ValuationDate { get; set; }
        }
    }
}