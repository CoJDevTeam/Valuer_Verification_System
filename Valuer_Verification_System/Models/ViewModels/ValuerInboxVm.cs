using Valuer_Verification_System.Models.Data;

namespace Valuer_Verification_System.Models.ViewModels
{
    public sealed class ValuerInboxVm
    {
        public string ValuerName { get; set; } = "";

        public int? BatchId { get; set; }
        public string? BatchName { get; set; }

        public string Filter { get; set; } = "Pending"; // Pending/Yes/No/All

        public int Total { get; set; }
        public int Pending { get; set; }
        public int Yes { get; set; }
        public int No { get; set; }

        public List<BatchOptionVm> Batches { get; set; } = new();
        public List<RowVm> Rows { get; set; } = new();

        public int UnreadNotifications { get; set; }
       
        public List<NotificationItemVm> Notifications { get; set; } = new();

        public sealed class NotificationItemVm
        {
            public long Id { get; set; }
            public string Title { get; set; } = "";
            public string Message { get; set; } = "";
            public string? ActionUrl { get; set; }
            public bool IsRead { get; set; }
            public DateTime CreatedAtUtc { get; set; }
        }

        public sealed class ValuerNotificationVm
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
            public int ExtractRowId { get; set; }
            public string ValuationKey { get; set; } = "";
            public string? Sector { get; set; }
            public string? PropertyDescription { get; set; }
            public decimal? MarketValue { get; set; }
            public string? Category { get; set; }
            public string? PropertyType { get; set; }

            public VerificationDecision Decision { get; set; }
            public DateTime? DecisionAtUtc { get; set; }
            public string? DecisionComment { get; set; }

            public string AssignedToValuer { get; set; } = "";
        }
    }
}