using Valuer_Verification_System.Models.Data;
using Valuer_Verification_System.Models.SLA;

namespace Valuer_Verification_System.Models.ViewModels
{
    public sealed class DataTeamDashboardVm
    {
        // Next action hero card
        public string NextActionTitle { get; set; } = "";
        public string NextActionHint { get; set; } = "";
        public string NextActionUrl { get; set; } = "";
        public string NextActionButton { get; set; } = "Continue";

        // Quick highlights
        public int ActiveWeekWindows { get; set; }
        public int TotalBatchesLast30 { get; set; }
        public int BatchesInVerification { get; set; }
        public int BatchesReadyToSend { get; set; }

        public List<WeekWindowCardVm> WeekWindows { get; set; } = new();
        public List<BatchCardVm> RecentBatches { get; set; } = new();

        public sealed class WeekWindowCardVm
        {
            public int Id { get; set; }
            public DateOnly WeekStart { get; set; }
            public DateOnly WeekEnd { get; set; }
            public DateOnly NoticeSendDate { get; set; }
            public DateOnly ReviewOpenDate { get; set; }
            public DateOnly ReviewCloseDate { get; set; }
            public WeekWindowStatus Status { get; set; }
        }

        public sealed class BatchCardVm
        {
            public int Id { get; set; }
            public string BatchName { get; set; } = "";
            public DateOnly BatchDate { get; set; }

            public DateOnly WeekStart { get; set; }
            public DateOnly WeekEnd { get; set; }
            public DateOnly NoticeSendDate { get; set; }
            public DateOnly ReviewOpenDate { get; set; }
            public DateOnly ReviewCloseDate { get; set; }

            public SlaStatus SlaStatus { get; set; }
            public int DaysToSend { get; set; }
            public string SlaLabel { get; set; } = "";

            public int Total { get; set; }
            public int Pending { get; set; }
            public int Yes { get; set; }
            public int No { get; set; }
            public decimal ProgressPercent { get; set; }

            public BatchStatus Status { get; set; }
            public bool ManagersNotified { get; set; }

            public bool NoticesSent { get; set; }
            public DateTime? NoticesSentAtUtc { get; set; }
            public string? NoticesSentBy { get; set; }

            public bool ReadyForNotices => Total > 0 && Pending == 0;
            public bool NoticesCanBeSent => ReadyForNotices
                                         && ManagersNotified
                                         && !NoticesSent
                                         && Status == BatchStatus.ReadyToSend;
        }
    }
}