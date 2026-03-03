using System.ComponentModel.DataAnnotations;

namespace Valuer_Verification_System.Models.Data
{
    public sealed class WeekWindow : EntityBase
    {
        // Always Monday
        public DateOnly WeekStart { get; set; }

        // Always Sunday (computed in app logic)
        public DateOnly WeekEnd { get; set; }

        // Friday of following week, or Thursday if Friday holiday (computed in app logic)
        public DateOnly NoticeSendDate { get; set; }

        // Last Friday of the same calendar month as NoticeSendDate.
        // If that Friday is a public holiday → last Thursday of the month.
        public DateOnly ReviewOpenDate { get; set; }

        // ReviewOpenDate + 60 calendar days.
        public DateOnly ReviewCloseDate { get; set; }

        public WeekWindowStatus Status { get; set; } = WeekWindowStatus.Draft;

        [MaxLength(512)]
        public string? Notes { get; set; }

        public List<Batch> Batches { get; set; } = new();
    }

    public sealed class Batch : EntityBase
    {
        [MaxLength(20)]
        public string BatchName { get; set; } = ""; // e.g. VVS_0001

        public DateOnly BatchDate { get; set; } // upload date (local date)

        public BatchStatus Status { get; set; } = BatchStatus.Uploaded;

        public int WeekWindowId { get; set; }
        public WeekWindow WeekWindow { get; set; } = null!;

        public ExtractFile ExtractFile { get; set; } = null!;

        public List<ExtractRow> Rows { get; set; } = new();

        public List<EmailArchive> Emails { get; set; } = new();
        public List<StatsFile> StatsFiles { get; set; } = new();

        public DateTime? ManagersNotifiedAtUtc { get; set; }
        public string? ManagersNotifiedBy { get; set; }

        // Notice send tracking (set by Data Team via Send Notices workflow)
        public bool NoticesSent { get; set; } = false;
        public DateTime? NoticesSentAtUtc { get; set; }
        public string? NoticesSentBy { get; set; }

    }

    public sealed class ExtractFile : EntityBase
    {
        public int BatchId { get; set; }
        public Batch Batch { get; set; } = null!;

        [MaxLength(255)]
        public string OriginalFileName { get; set; } = ""; // keep unchanged

        [MaxLength(1024)]
        public string SavedPath { get; set; } = ""; // Property Verifications\Extracts\...

        public long FileSizeBytes { get; set; }

        public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;

        [MaxLength(64)]
        public string? FileHashSha256 { get; set; } // optional (nice for integrity)
    }
}