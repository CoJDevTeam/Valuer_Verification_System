using System.ComponentModel.DataAnnotations;

namespace Valuer_Verification_System.Models.Data
{
    public sealed class CleaningStepAudit
    {
        public int Id { get; set; }

        public int? BatchId { get; set; }

        public int? ImportAuditId { get; set; }

        [MaxLength(100)]
        public string StepName { get; set; } = "";

        public DateTime StartedAtUtc { get; set; }

        public DateTime? FinishedAtUtc { get; set; }

        public long? DurationMs { get; set; }

        public bool Success { get; set; }

        public int RowsAffected { get; set; }

        [MaxLength(1000)]
        public string? Message { get; set; }

        public string? ErrorMessage { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}