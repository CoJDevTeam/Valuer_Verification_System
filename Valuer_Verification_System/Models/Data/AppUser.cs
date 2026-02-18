namespace Valuer_Verification_System.Models.Data
{
    public class AppUser
    {
        public int UserID { get; set; }
        public string Username { get; set; } = string.Empty;
        public string UsernameOld { get; set; } = string.Empty;

        /// <summary>Full display name (e.g. "John Mokoena").</summary>
        public string FullName { get; set; } = string.Empty;

        /// <summary>Role name from the Roles table: "Data Manager" | "Valuer" | "Valuer Manager"</summary>
        public string Role { get; set; } = string.Empty;

        public bool Active { get; set; }

        // ── Convenience role checks ──────────────────────────────────────────

        public bool IsDataManager => Role.Equals("Data Manager", StringComparison.OrdinalIgnoreCase);
        public bool IsValuer => Role.Equals("Valuer", StringComparison.OrdinalIgnoreCase);
        public bool IsValuerManager => Role.Equals("Valuer Manager", StringComparison.OrdinalIgnoreCase);
    }
}
