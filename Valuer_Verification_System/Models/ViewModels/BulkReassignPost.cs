namespace Valuer_Verification_System.Models.ViewModels
{
    public sealed class BulkReassignPost
    {
        public int BatchId { get; set; }
        public string FromValuer { get; set; } = "";
        public string ToValuer { get; set; } = "";
        public string Reason { get; set; } = "";

        // keep filters when redirecting
        public string? CurrentValuer { get; set; }
        public string? Sector { get; set; }
        public int Top { get; set; } = 500;
    }
}
