using System.ComponentModel.DataAnnotations;

namespace Valuer_Verification_System.Models.Data
{
    public sealed class ExtractFolderImportAudit
    {
        public int Id { get; set; }

        [MaxLength(50)]
        public string RunType { get; set; } = "";
        // Manual / Hosted

        [MaxLength(256)]
        public string StartedBy { get; set; } = "";

        public DateTime StartedAtUtc { get; set; }

        public DateTime? FinishedAtUtc { get; set; }

        public bool Success { get; set; }

        public int ImportedCount { get; set; }

        public int SkippedCount { get; set; }

        public int FailedCount { get; set; }

        [MaxLength(1000)]
        public string? Message { get; set; }

        public string? ErrorMessage { get; set; }

        [MaxLength(500)]
        public string? FolderPath { get; set; }

        [MaxLength(128)]
        public string? MachineName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
