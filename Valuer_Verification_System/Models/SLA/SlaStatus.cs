namespace Valuer_Verification_System.Models.SLA
{
    public enum SlaStatus
    {
        OnTrack = 1,   // green
        AtRisk = 2,    // amber
        Overdue = 3,   // red
        Completed = 4  // blue/green (pending = 0)
    }
}
