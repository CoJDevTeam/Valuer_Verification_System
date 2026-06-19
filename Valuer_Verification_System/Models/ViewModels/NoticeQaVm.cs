namespace Valuer_Verification_System.Models.ViewModels
{
    public sealed class NoticeQaVm
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = "";
        public string WeekTableName { get; set; } = "";
        public string PrintFolder { get; set; } = "";

        public int TotalPrintedNotices { get; set; }
        public int TotalSampled { get; set; }

        public bool IsApproved { get; set; }
        public string? ApprovedBy { get; set; }
        public string? ApprovedByName { get; set; }
        public DateTime? ApprovedAtUtc { get; set; }
        public string? Comments { get; set; }

        public List<NoticeQaCategoryVm> Categories { get; set; } = new();
    }

    public sealed class NoticeQaCategoryVm
    {
        public string Category { get; set; } = "";
        public int TotalNotices { get; set; }
        public int SampleSize { get; set; }

        public List<NoticeQaSampleVm> Samples { get; set; } = new();
    }

    public sealed class NoticeQaSampleVm
    {
        public int Id { get; set; }
        public int SampleNo { get; set; }
        public string ValuationKey { get; set; } = "";
        public string PropertyDescription { get; set; } = "";
        public string Category { get; set; } = "";
        public bool PdfExists { get; set; }
    }
}