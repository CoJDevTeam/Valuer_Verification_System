using System.ComponentModel.DataAnnotations;

namespace Valuer_Verification_System.Models.Data
{

    public enum NotificationAudience
    {
        Valuer = 1,
        Manager = 2,
        DataTeam = 3
    }

    public enum NotificationType
    {
        ReminderPending = 1,
        BatchAtRisk = 2,
        BatchOverdue = 3,
        ExtractUploaded = 4,
        StatsReady = 5
    }

    public sealed class Notification
    {
        public long Id { get; set; }

        // Who should see it
        public NotificationAudience Audience { get; set; }

        // For Valuer audience we target a specific “user key”
        // For now, use AssignedToValuer string (later map to Users table)
        [MaxLength(200)]
        public string? TargetKey { get; set; }

        public NotificationType Type { get; set; }

        [MaxLength(200)]
        public string Title { get; set; } = "";

        [MaxLength(2000)]
        public string Message { get; set; } = "";

        [MaxLength(500)]
        public string? ActionUrl { get; set; } // link to inbox/manager dashboard

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public bool IsRead { get; set; }
        public DateTime? ReadAtUtc { get; set; }

        // ✅ Prevent spam: unique key per day per target
        [MaxLength(200)]
        public string DedupKey { get; set; } = "";
    }
}

