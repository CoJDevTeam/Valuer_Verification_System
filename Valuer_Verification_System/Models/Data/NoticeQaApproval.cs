using System.ComponentModel.DataAnnotations;

namespace Valuer_Verification_System.Models.Data
{
    public sealed class NoticeQaApproval : EntityBase
    {
        public int BatchId { get; set; }
        public Batch Batch { get; set; } = null!;

        [MaxLength(255)]
        public string WeekTableName { get; set; } = "";

        [MaxLength(1024)]
        public string PrintFolder { get; set; } = "";

        public int TotalPrintedNotices { get; set; }
        public int TotalSampled { get; set; }

        public bool IsApproved { get; set; }

        [MaxLength(128)]
        public string? ApprovedBy { get; set; }

        [MaxLength(255)]
        public string? ApprovedByName { get; set; }

        public DateTime? ApprovedAtUtc { get; set; }

        [MaxLength(2000)]
        public string? Comments { get; set; }

        public DateTime? DataManagerEmailSentAtUtc { get; set; }

        public List<NoticeQaSample> Samples { get; set; } = new();
    }

    public sealed class NoticeQaSample : EntityBase
    {
        public int NoticeQaApprovalId { get; set; }
        public NoticeQaApproval NoticeQaApproval { get; set; } = null!;

        public int BatchId { get; set; }

        [MaxLength(64)]
        public string ValuationKey { get; set; } = "";

        [MaxLength(150)]
        public string Category { get; set; } = "";

        [MaxLength(512)]
        public string PropertyDescription { get; set; } = "";

        [MaxLength(1024)]
        public string PdfPath { get; set; } = "";

        public bool PdfExists { get; set; }

        public int SampleNo { get; set; }
        public int CategoryTotal { get; set; }
        public int CategorySampleSize { get; set; }
    }
}