namespace Valuer_Verification_System.Infrastructure
{
    /// <summary>
    /// One rule for "is this extract cleaned and ready for approval".
    /// It is cleaned when a cleaned time is stored and the status is not NotCleaned, Running or Failed
    /// (so "Cleaned", "Imported" and "ReadyToSend" all count).
    /// </summary>
    public static class CleaningStatus
    {
        public static bool IsDone(string? status, DateTime? cleanedAtUtc)
        {
            if (cleanedAtUtc == null) return false;

            var s = (status ?? "").Trim();
            return !(s.Equals("NotCleaned", StringComparison.OrdinalIgnoreCase)
                  || s.Equals("Running", StringComparison.OrdinalIgnoreCase)
                  || s.Equals("Failed", StringComparison.OrdinalIgnoreCase));
        }
    }
}
