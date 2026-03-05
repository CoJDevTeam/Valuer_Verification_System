using System.ComponentModel.DataAnnotations;

namespace Valuer_Verification_System.Models.Data
{
    public abstract class EntityBase
    {
        public int Id { get; set; }

        [MaxLength(128)]
        public string CreatedBy { get; set; } = "system";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(128)]
        public string? UpdatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }

    public enum WeekWindowStatus
    {
        Draft = 0,
        Confirmed = 1,
        Uploaded = 2,
        InVerification = 3,
        ReadyToSend = 4,
        Sent = 5,
        Cancelled = 6
    }

    public enum BatchStatus
    {
        Uploaded = 0,
        InVerification = 1,
        ReadyToSend = 2,
        Sent = 3,
        Cancelled = 4,
        PrintedEmailPending=5
    }

    public enum VerificationDecision
    {
        Pending = 0,
        Yes = 1,
        No = 2
    }

    public enum EmailKind
    {
        UploadReadyToManager = 1,
        ClientNotice = 2
    }

    public enum StatsKind
    {
        BatchStats = 1,
        WeeklyCombinedStats = 2
    }
}
