namespace Valuer_Verification_System.Models.ViewModels
{
    public sealed class UploadExtractVm
    {
        public int WeekWindowId { get; set; }

        public IFormFile? File { get; set; }

        // For dropdown
        public List<WeekWindowOptionVm> WeekWindows { get; set; } = new();

        public sealed class WeekWindowOptionVm
        {
            public int Id { get; set; }
            public DateOnly WeekStart { get; set; }
            public DateOnly WeekEnd { get; set; }
            public DateOnly NoticeSendDate { get; set; }
        }
    }
}
