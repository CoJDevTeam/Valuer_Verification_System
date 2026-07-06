using Valuer_Verification_System.Models.Data;

namespace Valuer_Verification_System.Models.ViewModels
{
    public sealed class ValuerInboxVm
    {
        public string ValuerName { get; set; } = "";
        public int? BatchId { get; set; }
        public string? BatchName { get; set; }
        public string Filter { get; set; } = "Pending";

        // Header stat chips
        public int Total { get; set; }
        public int Pending { get; set; }
        public int Yes { get; set; }
        public int No { get; set; }
        public int BulkGroupCount { get; set; }

        public List<BatchOptionVm> Batches { get; set; } = new();
        public List<RowVm> Rows { get; set; } = new();
        public int UnreadNotifications { get; set; }
        public List<NotificationItemVm> Notifications { get; set; } = new();

        // ── Nested VMs ────────────────────────────────────────────────────
        public sealed class NotificationItemVm
        {
            public long Id { get; set; }
            public string Title { get; set; } = "";
            public string Message { get; set; } = "";
            public string? ActionUrl { get; set; }
            public bool IsRead { get; set; }
            public DateTime CreatedAtUtc { get; set; }
        }

        public sealed class BatchOptionVm
        {
            public int Id { get; set; }
            public string BatchName { get; set; } = "";
            public DateOnly BatchDate { get; set; }
            public DateOnly WeekStart { get; set; }
            public DateOnly WeekEnd { get; set; }
            public DateOnly NoticeSendDate { get; set; }
        }

        public sealed class RowVm
        {
            // ── Core ──────────────────────────────────────────────────────
            public int ExtractRowId { get; set; }
            public string ValuationKey { get; set; } = "";
            public string Premise_ID { get; set; } = "";
            public string? Sector { get; set; }
            public string UnitType { get; set; } = "";
            public string? PropertyDescription { get; set; }
            public decimal? MarketValue { get; set; }
            public string? Category { get; set; }

            // ── Decision ──────────────────────────────────────────────────
            public VerificationDecision Decision { get; set; }
            public DateTime? DecisionAtUtc { get; set; }
            public string? DecisionComment { get; set; }

            // ── Bulk group fields (IsBulkGroup = true only) ───────────────
            //
            //   IsBulkGroup   — true  → sectional title group (ST+SL+FL share one UnitKey)
            //                   false → freehold / standalone row
            //
            //   BulkRowCount  — total number of linked records in this group
            //   BulkPendingCount/YesCount/NoCount — per-status breakdown
            //   BulkUnitKey   — the grouping key used by ValuerBulkController
            //
            public bool IsBulkGroup { get; set; }
            public int BulkRowCount { get; set; }
            public int BulkPendingCount { get; set; }
            public int BulkYesCount { get; set; }
            public int BulkNoCount { get; set; }
            public string BulkUnitKey { get; set; } = "";
            public string BulkPremiseId { get; set; } = "";
            public string BulkUnitNo { get; set; } = "";
            public string BulkTownNameDesc { get; set; } = "";

            // Convenience display text
            public string TypeLabel =>
                IsBulkGroup
                    ? $"ST Group ({BulkRowCount})"
                    : "Single";
        }
    }
}