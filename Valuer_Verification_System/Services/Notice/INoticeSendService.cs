using Valuer_Verification_System.Models.Notice;

namespace Valuer_Verification_System.Services.Notice
{
    public interface INoticeSendService
    {
        /// <summary>
        /// Email archive folder = print folder + "_EMAIL" suffix.
        /// e.g. C:\...\GV23_LIS_SV_2026_02_EXTRACT_W2_EMAIL
        /// </summary>
        string BuildEmailFolder(DateOnly extractionDate, string batchName, string weekTableName);

        Task<SendChunkResult> SendChunkAsync(
            int batchId,
            string weekTableName,
            int offset,
            int chunkSize,
            CancellationToken ct);
    }

    public sealed class SendChunkResult
    {
        public bool Success { get; set; }
        public int Sent { get; set; }
        public int Total { get; set; }
        public bool Done { get; set; }
        public string? CurrentProperty { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}