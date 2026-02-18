namespace Valuer_Verification_System.Models.ViewModels
{
    public sealed class WeekWindowCreateVm
    {
        // Option A: user selects any date
        public DateOnly SelectedDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    }

    public sealed class WeekWindowDetailsVm
    {
        public int Id { get; set; }
        public DateOnly WeekStart { get; set; }
        public DateOnly WeekEnd { get; set; }
        public DateOnly NoticeSendDate { get; set; }
        public string Status { get; set; } = "Draft";
    }
}
