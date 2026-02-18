using Valuer_Verification_System.Models.SLA;

namespace Valuer_Verification_System.Models.ViewModels
{
    public sealed class ManagerDashboardVm
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = "";
        public DateOnly BatchDate { get; set; }

        public DateOnly WeekStart { get; set; }
        public DateOnly WeekEnd { get; set; }
        public DateOnly NoticeSendDate { get; set; }

        public int Total { get; set; }
        public int Pending { get; set; }
        public int Yes { get; set; }
        public int No { get; set; }

        public decimal ProgressPercent { get; set; } // Verified/Total * 100

        public bool ManagersNotified { get; set; }
        public DateTime? ManagersNotifiedAtUtc { get; set; }

        public List<BatchOptionVm> Batches { get; set; } = new();
        public List<ValuerBreakdownVm> ByValuer { get; set; } = new();
        public List<SectorBreakdownVm> BySector { get; set; } = new();

        public bool ReadyForNotices => Pending == 0 && Total > 0;
        public string ReadyMessage => ReadyForNotices
            ? "All records verified. Batch is ready for the Notice step."
            : "Pending records must be 0 before notices can be generated/sent.";

        public SlaStatus SlaStatus { get; set; }
        public int DaysToSend { get; set; }
        public string SlaLabel { get; set; } = "";

        public sealed class BatchOptionVm
        {
            public int Id { get; set; }
            public string BatchName { get; set; } = "";
            public DateOnly BatchDate { get; set; }
        }

        public sealed class ValuerBreakdownVm
        {
            public string Valuer { get; set; } = "";
            public int Total { get; set; }
            public int Pending { get; set; }
            public int Yes { get; set; }
            public int No { get; set; }
            public decimal ProgressPercent { get; set; }
        }

        public sealed class SectorBreakdownVm
        {
            public string Sector { get; set; } = "";
            public int Total { get; set; }
            public int Pending { get; set; }
            public int Yes { get; set; }
            public int No { get; set; }
            public decimal ProgressPercent { get; set; }
        }
    }
}