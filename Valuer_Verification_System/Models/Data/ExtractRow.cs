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
