using Valuer_Verification_System.Models.Data;
using Valuer_Verification_System.Models.SLA;

namespace Valuer_Verification_System.Models.ViewModels
{
    public sealed class DataTeamDashboardVm
    {
        // ── Section 78 Stats embedded in dashboard ──────────────────────────
        public Section78StatsVm Stats { get; set; } = new();

        // ── Next action hero card ───────────────────────────────────────────
        public string NextActionTitle { get; set; } = "";
        public string NextActionHint { get; set; } = "";
        public string NextActionUrl { get; set; } = "";
        public string NextActionButton { get; set; } = "Continue";

        // ── Quick highlights ────────────────────────────────────────────────
        public int ActiveWeekWindows { get; set; }
        public int TotalBatchesLast30 { get; set; }

        // New meaning: batches released to sector managers / valuers
        public int BatchesInVerification { get; set; }

        // Batches where verification is complete and notice step can continue
        public int BatchesReadyToSend { get; set; }

        // New folder-import lifecycle KPIs
        public int CleanedBatches { get; set; }
        public int ProjectManagerApprovalPending { get; set; }
        public int SectorNotificationPending { get; set; }
        public int UnmatchedValuerRows { get; set; }

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

            public BatchStatus Status { get; set; }

            // ── Source extract file ─────────────────────────────────────────
            public string? OriginalFileName { get; set; }
            public string? SourceFilePath { get; set; }
            public DateTime? UploadedAtUtc { get; set; }

            // ── Cleaning lifecycle ──────────────────────────────────────────
            public string? ExtractCleaningStatus { get; set; }
            public DateTime? ExtractCleanedAtUtc { get; set; }
            public string? ExtractCleanedBy { get; set; }
            public string? CleanWeekTableName { get; set; }
            public string? ExtractCleaningMessage { get; set; }

            public bool IsCleaned =>
                Valuer_Verification_System.Infrastructure.CleaningStatus.IsDone(ExtractCleaningStatus, ExtractCleanedAtUtc);

            public bool CleaningFailed =>
                string.Equals(ExtractCleaningStatus, "Failed", StringComparison.OrdinalIgnoreCase);

            // ── QA + Project Manager notification ───────────────────────────
            public DateTime? ProjectManagerNotifiedAtUtc { get; set; }
            public string? ProjectManagerNotifiedTo { get; set; }
            public string? QaNotifiedTo { get; set; }

            public bool ProjectManagerNotified => ProjectManagerNotifiedAtUtc != null;

            // ── Project Manager approval ────────────────────────────────────
            public DateTime? ProjectManagerApprovedAtUtc { get; set; }
            public string? ProjectManagerApprovedBy { get; set; }
            public string? ProjectManagerApprovalComment { get; set; }

            public bool ProjectManagerApproved => ProjectManagerApprovedAtUtc != null;

            // ── Sector Manager release ──────────────────────────────────────
            public bool ManagersNotified { get; set; }
            public DateTime? ManagersNotifiedAtUtc { get; set; }
            public string? ManagersNotifiedBy { get; set; }

            // ── Verification counts ─────────────────────────────────────────
            public int Total { get; set; }
            public int Pending { get; set; }
            public int Yes { get; set; }
            public int No { get; set; }

            public int Verified => Yes + No;

            public decimal ProgressPercent { get; set; }

            public int UnmatchedValuerRows { get; set; }

            // ── SLA ─────────────────────────────────────────────────────────
            public SlaStatus SlaStatus { get; set; }
            public int DaysToSend { get; set; }
            public string SlaLabel { get; set; } = "";

            // ── Notice status ───────────────────────────────────────────────
            public bool NoticesSent { get; set; }
            public DateTime? NoticesSentAtUtc { get; set; }
            public string? NoticesSentBy { get; set; }

            // Verification can only start after PM approval and Sector Manager notification
            public bool ReleasedForVerification =>
                ProjectManagerApproved && ManagersNotified;

            // Ready for notices means:
            // 1. Batch has records
            // 2. Sector Managers were notified
            // 3. All records verified
            // 4. Notices not already sent
            public bool ReadyForNotices =>
                Total > 0 &&
                ManagersNotified &&
                Pending == 0 &&
                !NoticesSent;

            // This controls whether the Data Team can move to Send Notice
            public bool NoticesCanBeSent =>
                ReadyForNotices &&
                (
                    Status == BatchStatus.ReadyToSend ||
                    Status == BatchStatus.PrintedEmailPending ||
                    Status == BatchStatus.InVerification
                );
        }
    }
}