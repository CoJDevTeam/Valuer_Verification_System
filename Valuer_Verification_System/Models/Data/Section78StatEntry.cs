using System.ComponentModel.DataAnnotations;

namespace Valuer_Verification_System.Models.Data
{
    public sealed class Section78StatEntry : EntityBase
    {
        // ── Financial year key ────────────────────────────────────────────────
        [MaxLength(10)]
        public string FinancialYear { get; set; } = "2025_26"; // e.g. "2025_26"

        // ── Period + week table linkage ───────────────────────────────────────
        [MaxLength(50)]
        public string Period { get; set; } = "";             // e.g. "February 2026"

        public DateOnly? ExtractionDate { get; set; }        // e.g. 2026-02-09

        [MaxLength(100)]
        public string SvCapturePeriod { get; set; } = "";   // e.g. "2026/02/02 - 2026/02/08"

        [MaxLength(100)]
        public string? WeekTableName { get; set; }          // e.g. "GV23_LIS_SV_2026_02_EXTRACT_W2"

        // ── Counts (auto-populated from ExtractRows / week table) ─────────────
        public int TotalSvEntries { get; set; }
        public int OrdinaryEntries { get; set; }
        public int SplitValuesEntries { get; set; }
        public int Leases { get; set; }
        public int NoticesSuitableForPosting { get; set; }
        public int RemovedEntries { get; set; }
        public int EntriesWithZeroExtent { get; set; }
        public int NoticesNotSuitable { get; set; }
        public int SuitableForEmailing { get; set; }
        public int SentByEmailNoPostal { get; set; }
        public int MunicipalOwnedProperties { get; set; }

        // ── Editable dates ────────────────────────────────────────────────────
        public DateOnly? ReviewPeriodOpen { get; set; }
        public DateOnly? ReviewPeriodClosed { get; set; }

        // ── Summary fields matching the email screenshot ──────────────────────
        [MaxLength(5)]
        public string OpenYN { get; set; } = "Y";           // always Y

        public int? ReviewsReceived { get; set; }

        [MaxLength(5)]
        public string? Closed { get; set; }                 // Y / N

        // ── Audit ─────────────────────────────────────────────────────────────
        [MaxLength(128)]
        public string CompiledBy { get; set; } = "";

        // Linked batch (optional — for drill-down)
        public int? BatchId { get; set; }
    }
}
