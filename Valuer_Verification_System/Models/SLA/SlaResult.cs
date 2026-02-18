namespace Valuer_Verification_System.Models.SLA
{
    public sealed class SlaResult
    {
        public SlaStatus Status { get; set; }
        public int DaysToSend { get; set; } // negative = overdue
        public string Label { get; set; } = "";
    }
}
