namespace Valuer_Verification_System.Models.ViewModels
{
    public sealed class UploadReviewVm
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = "";
        public DateOnly BatchDate { get; set; }

        public int WeekWindowId { get; set; }
        public DateOnly WeekStart { get; set; }
        public DateOnly WeekEnd { get; set; }
        public DateOnly NoticeSendDate { get; set; }

        public int TotalRecords { get; set; }
        public int AssignedRecords { get; set; }

        public bool ManagersNotified { get; set; }
        public DateTime? ManagersNotifiedAtUtc { get; set; }

        public List<KeyValuePair<string, int>> SectorCounts { get; set; } = new();
        public List<KeyValuePair<string, int>> ValuerCounts { get; set; } = new();
    }

}
