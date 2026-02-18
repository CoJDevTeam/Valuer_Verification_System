using System.ComponentModel.DataAnnotations;

namespace Valuer_Verification_System.Models.Data
{
    public sealed class EmailArchive : EntityBase
    {
        public int BatchId { get; set; }
        public Batch Batch { get; set; } = null!;

        public EmailKind Kind { get; set; }

        [MaxLength(320)]
        public string ToAddress { get; set; } = "";

        [MaxLength(320)]
        public string FromAddress { get; set; } = "";

        [MaxLength(255)]
        public string Subject { get; set; } = "";

        [MaxLength(1024)]
        public string EmlPath { get; set; } = ""; // Property Verifications\Extracts\... .eml or Notices\... .eml

        public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;

        [MaxLength(50)]
        public string? Sector { get; set; } // useful for upload-ready emails per sector
    }

    public sealed class StatsFile : EntityBase
    {
        public int BatchId { get; set; }
        public Batch Batch { get; set; } = null!;

        public StatsKind Kind { get; set; }

        [MaxLength(1024)]
        public string XlsxPath { get; set; } = ""; // Property Verifications\Stats\...

        public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;

        [MaxLength(64)]
        public string Status { get; set; } = "Success"; // Success/Failed

        [MaxLength(2000)]
        public string? Error { get; set; }
    }
}
