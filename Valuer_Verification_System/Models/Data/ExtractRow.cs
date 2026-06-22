using System.ComponentModel.DataAnnotations;

namespace Valuer_Verification_System.Models.Data
{

    public sealed class ExtractRow : EntityBase
    {
        // ── Batch relationship ──────────────────────────────────────────────
        public int BatchId { get; set; }
        public Batch Batch { get; set; } = null!;

        // ── Primary key field ───────────────────────────────────────────────
        [MaxLength(64)]
        public string ValuationKey { get; set; } = "";          // VALUATION_KEY

        // ── Workflow / verification ─────────────────────────────────────────
        public VerificationDecision Decision { get; set; } = VerificationDecision.Pending;
        public DateTime? DecisionAtUtc { get; set; }

        [MaxLength(2000)]
        public string? DecisionComment { get; set; }

        public Assignment? Assignment { get; set; }             // 1:1

        // ══════════════════════════════════════════════════════════════════
        // SECTION 1 – Core extract columns (present in the weekly XLS file)
        // ══════════════════════════════════════════════════════════════════

        public DateOnly? ExtractionDate { get; set; }           // EXTRACTION_DATE
        public string? TownNameDesc { get; set; }               // TOWN_NAME_DESC
        public string? PropertyId { get; set; }                 // PROPERTY_ID
        public string? SgId { get; set; }                       // SG_ID
        public string? StandNo { get; set; }                    // STAND_NO
        public string? StatusSubtype { get; set; }              // STATUS_SUBTYPE

        public string? UnitKey { get; set; }                    // UNIT_KEY
        public string? UnitType { get; set; }                   // UNIT_TYPE
        public string? UnitNo { get; set; }                     // UNIT_NO
        public string? LegalArea { get; set; }                  // LEGAL_AREA

        public string? UnitRightsKey { get; set; }              // UNIT_RIGHTS_KEY
        public string? RightsPremise { get; set; }              // RIGHTS_PREMISE
        public string? RightsUnitType { get; set; }             // RIGHTS_UNIT_TYPE  (was missing)
        public string? RightDescription { get; set; }           // RIGHT_DESCRIPTION
        public decimal? RightsPremiseSize { get; set; }         // RIGHTS_PREMISE_SIZE
        public string? RightsStatus { get; set; }               // RIGHTS_STATUS

        public string? ValuationKey2 { get; set; }              // VALUATION_KEY2  (was missing)
        public string? ValuationRollKey { get; set; }           // VALUATION_ROLL_KEY
        public string? ValuationType { get; set; }              // VALUATION_TYPE
        public DateOnly? ValuationDate { get; set; }            // VALUATION_DATE
        public DateOnly? WefDate { get; set; }                  // WEF_DATE
        public string? StatusCode { get; set; }                 // STATUS_CODE

        public decimal? RateableArea { get; set; }              // RATEABLE_AREA
        public decimal? MarketValue { get; set; }               // MARKET_VALUE

        public string? ValuationSplitNo { get; set; }           // VALUATION_SPLIT_NO
        public string? ValuationSplitInd { get; set; }          // VALUATION_SPLIT_IND

        public string? CatCode { get; set; }                    // CAT_CODE
        public string? CatDesc { get; set; }                    // CAT_DESC

        public string? Sector { get; set; }                     // SECTOR
        public string? Valuer { get; set; }                     // VALUER

        // Resolved valuer from UserManagement.dbo.Users.
        // This becomes the new source for valuer inbox assignment.
        [MaxLength(256)]
        public string? Username { get; set; }

        public int? ResolvedUserId { get; set; }

        [MaxLength(256)]
        public string? ResolvedValuerEmail { get; set; }

        [MaxLength(50)]
        public string? ValuerMatchStatus { get; set; }

        [MaxLength(500)]
        public string? ValuerMatchComment { get; set; }

        [MaxLength(255)]
        public string? SourceFileName { get; set; }

        [MaxLength(64)]
        public string? SourceFileHashSha256 { get; set; }

        public int? SourceRowNumber { get; set; }

        // SendNotices Y/N  (also stored as "SEND NOTICE?" in some file versions)
        public string? SendNotice { get; set; }                 // SendNotices Y/N

        // ── Convenience / derived fields ────────────────────────────────────
        [MaxLength(512)]
        public string? PropertyDescription { get; set; }        // PROPERTY_DESCRIPTION (or PROPERTY_DESC)

        [MaxLength(100)]
        public string? PropertyType { get; set; }               // PROPERTY_CATEGORY

        [MaxLength(100)]
        public string? Category { get; set; }                   // CAT_DESC fallback

        // ══════════════════════════════════════════════════════════════════
        // SECTION 2 – Extended LIS columns (Week Table / Notice feature)
        //             Populated from GV23_LIS_SV_YYYY_MM_EXTRACT_Wn
        // ══════════════════════════════════════════════════════════════════

        // Erf / Portion identifiers
        public string? Erf { get; set; }                        // ERF
        public string? Ptn { get; set; }                        // PTN
        public string? Re { get; set; }                         // RE
        public string? PremiseId { get; set; }                  // PREMISE_ID

        // Notice / review dates
        public string? NoticeOpen { get; set; }                 // NOTICE_OPEN
        public string? ExtractPeriod { get; set; }              // EXTRACT_PERIOD
        public DateOnly? LetterDate { get; set; }               // LETTER_DATE
        public DateOnly? ReviewOpenDate { get; set; }           // REVIEW_OPEN_DATE
        public DateOnly? ReviewCloseDate { get; set; }          // REVIEW_CLOSE_DATE
        public string? ReviewOpen { get; set; }                 // REVIEW_OPEN
        public DateOnly? ReviewCloseDate2 { get; set; }         // REVIEW_CLOSE_DATE2
        public DateOnly? ReviewOpenDate2 { get; set; }          // REVIEW_OPEN_DATE2

        // Scheme info
        public string? SchemeName { get; set; }                 // Scheme_Name
        public string? SchemeNo { get; set; }                   // Scheme_No
        public string? SchemeYear { get; set; }                 // Scheme_Year
        public string? SchemeNameDesc { get; set; }             // SCHEME_NAME_DESC
        public string? SchemeNumber { get; set; }               // SCHEME_NUMBER

        // Property descriptions (LIS extended)
        public string? StPropertyDescription { get; set; }     // ST_PROPERTY_DESCRIPTION
        public string? PropertyDescription_ { get; set; }      // PROPERTY_DESCRIPTION_
        public string? PropertyDesc { get; set; }               // PROPERTY_DESC
        public string? PropertyCategory { get; set; }          // PROPERTY_CATEGORY
        public string? LeasePropertyDesc { get; set; }         // LEASE_PROP_DESC

        // Owner / correspondence
        public string? OwnerName { get; set; }                  // OwnerS_Name / OWNER_NAME
        public string? Addr1 { get; set; }                      // ADDR1
        public string? Addr2 { get; set; }                      // ADDR2
        public string? Addr3 { get; set; }                      // ADDR3
        public string? Addr4 { get; set; }                      // ADDR4
        public string? Addr5 { get; set; }                      // ADDR5
        public string? PremiseAddress { get; set; }             // PREMISE_ADDRESS
        public string? LisStreetAddress { get; set; }           // LIS_STREET_ADDRESS
        public string? PhysicalAddress { get; set; }            // PHYSICAL_ADDRESS
        public string? StAddr2Postal { get; set; }              // ST_ADDR2_POSTAL
        public string? UnitNoChar { get; set; }                 // UNIT_NO_CHAR

        // Contact / email
        public string? EmailInd { get; set; }                   // EMAIL_IND
        public string? EmailAddr { get; set; }                  // EMAIL_ADDR

        // COJ / SAP flags
        public string? CojOwned { get; set; }                   // COJ_OWNED
        public string? SapData { get; set; }                    // SAP_DATA
        public string? Usable { get; set; }                     // USABLE
        public string? SplitPost { get; set; }                  // SPLIT_POST

        // Remarks / misc
        public string? RemarkDesc { get; set; }                 // REMARK_DESC
        public string? Remarks { get; set; }                    // REMARKS
        public string? Extent { get; set; }                     // EXTENT
        public string? EStatus { get; set; }                    // eSTATUS
        public string? LeaseUnitNo { get; set; }                // LEASE_UNIT_NO

        // VALUER_EMAIL
       

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
