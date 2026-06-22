namespace Valuer_Verification_System.Models.ViewModels
{
    public sealed class ExtractFolderImportAuditVm
    {
        public int Id { get; set; }

        public string RunType { get; set; } = "";
        public string StartedBy { get; set; } = "";

        public DateTime StartedAtUtc { get; set; }
        public DateTime? FinishedAtUtc { get; set; }

        public bool Success { get; set; }

        public int ImportedCount { get; set; }
        public int SkippedCount { get; set; }
        public int FailedCount { get; set; }

        public string? Message { get; set; }
        public string? ErrorMessage { get; set; }

        public string? FolderPath { get; set; }
        public string? MachineName { get; set; }

        public string DurationDisplay
        {
            get
            {
                if (FinishedAtUtc == null)
                    return "Running";

                var duration = FinishedAtUtc.Value - StartedAtUtc;

                if (duration.TotalSeconds < 60)
                    return $"{duration.TotalSeconds:N0}s";

                return $"{duration.TotalMinutes:N1} min";
            }
        }
    }
}
