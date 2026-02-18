namespace Valuer_Verification_System.Models.ViewModels
{
    public sealed class WeekWindowVm
    {
        public DateOnly SelectedDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        public DateOnly WeekStart { get; set; }
        public DateOnly WeekEnd { get; set; }
        public DateOnly NoticeSendDate { get; set; }

        public string Status { get; set; } = "Draft";
    }
}
