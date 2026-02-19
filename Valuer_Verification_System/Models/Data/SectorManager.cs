using System.ComponentModel.DataAnnotations;

namespace Valuer_Verification_System.Models.Data
{
    public sealed class SectorManager
    {
        public int Id { get; set; }

        // If you want “All sectors” managers too, use Sector="*"
        [Required, MaxLength(80)]
        public string Sector { get; set; } = "*";

        [Required, MaxLength(200)]
        public string FullName { get; set; } = "";

        [Required, MaxLength(320)]
        public string Email { get; set; } = "";

        public bool IsActive { get; set; } = true;

        public string CreatedBy { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
