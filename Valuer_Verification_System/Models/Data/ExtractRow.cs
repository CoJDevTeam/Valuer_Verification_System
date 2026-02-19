using System.ComponentModel.DataAnnotations;

namespace Valuer_Verification_System.Models.Data
{

    public sealed class ExtractRow : EntityBase
    {
        public int BatchId { get; set; }
        public Batch Batch { get; set; } = null!;

        // Key fields (from your extract)
        [MaxLength(64)]
        public string ValuationKey { get; set; } = "";     // important ID

        [MaxLength(50)]
        public string? Sector { get; set; }                // North/South/East/West etc

        [MaxLength(128)]
        public string? Valuer { get; set; }                // assignment source from sheet

        // Useful valuation fields (keep minimal now; can add more later)
        public decimal? MarketValue { get; set; }

        [MaxLength(255)]
        public string? PropertyDescription { get; set; }

        [MaxLength(50)]
        public string? PropertyType { get; set; }

        [MaxLength(50)]
        public string? Category { get; set; }

        // Raw dates from sheet (if present)
        public DateOnly? ExtractionDate { get; set; }

        // Normalised workflow
        public VerificationDecision Decision { get; set; } = VerificationDecision.Pending;

        public DateTime? DecisionAtUtc { get; set; }

        // If Decision = No, comment is required (enforced at app/service level)
        [MaxLength(2000)]
        public string? DecisionComment { get; set; }

        public Assignment? Assignment { get; set; } // 1:1

        public int Id { get; set; }

 

        // =========================
        // ✅ Excel columns (match file)
        // =========================
                      // EXTRACTION_DATE
        public string? TownNameDesc { get; set; }                      // TOWN_NAME_DESC
        public string? PropertyId { get; set; }                        // PROPERTY_ID
        public string? SgId { get; set; }                              // SG_ID
        public string? StandNo { get; set; }                           // STAND_NO
        public string? StatusSubtype { get; set; }                     // STATUS_SUBTYPE

        public string? UnitKey { get; set; }                           // UNIT_KEY
        public string? UnitType { get; set; }                          // UNIT_TYPE
        public string? UnitNo { get; set; }                            // UNIT_NO
        public string? LegalArea { get; set; }                         // LEGAL_AREA

        public string? UnitRightsKey { get; set; }                     // UNIT_RIGHTS_KEY
        public string? RightsPremise { get; set; }                     // RIGHTS_PREMISE
        public string? RightDescription { get; set; }                  // RIGHT_DESCRIPTION
        public decimal? RightsPremiseSize { get; set; }                // RIGHTS_PREMISE_SIZE
        public string? RightsStatus { get; set; }                      // RIGHTS_STATUS

                  // VALUATION_KEY

        public string? ValuationRollKey { get; set; }                  // VALUATION_ROLL_KEY
        public string? ValuationType { get; set; }                     // VALUATION_TYPE
        public DateOnly? ValuationDate { get; set; }                   // VALUATION_DATE
        public DateOnly? WefDate { get; set; }                         // WEF_DATE
        public string? StatusCode { get; set; }                        // STATUS_CODE

        public decimal? RateableArea { get; set; }                     // RATEABLE_AREA
                     // MARKET_VALUE

        public string? ValuationSplitNo { get; set; }                  // VALUATION_SPLIT_NO
        public string? ValuationSplitInd { get; set; }                 // VALUATION_SPLIT_IND

        public string? CatCode { get; set; }                           // CAT_CODE
        public string? CatDesc { get; set; }                           // CAT_DESC

                           // SECTOR
        public string? SendNotice { get; set; }                        // SEND NOTICE?
     
        // =========================
        // ✅ Workflow fields
        // =========================
 

        // Auditing
        public string CreatedBy { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public sealed class Assignment : EntityBase
    {
        public int ExtractRowId { get; set; }
        public ExtractRow ExtractRow { get; set; } = null!;

        // Current assignee. Start from ExtractRow.Valuer.
        [MaxLength(128)]
        public string AssignedToValuer { get; set; } = "";

        public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;

        [MaxLength(128)]
        public string AssignedBy { get; set; } = "system"; // manager username or system

        public List<AssignmentHistory> History { get; set; } = new();
    }

    public sealed class AssignmentHistory : EntityBase
    {
        public int AssignmentId { get; set; }
        public Assignment Assignment { get; set; } = null!;

        [MaxLength(128)]
        public string FromValuer { get; set; } = "";

        [MaxLength(128)]
        public string ToValuer { get; set; } = "";

        [MaxLength(128)]
        public string ChangedBy { get; set; } = ""; // manager

        public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;

        [MaxLength(512)]
        public string? Reason { get; set; }
    }
}
