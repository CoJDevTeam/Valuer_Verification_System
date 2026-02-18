namespace Valuer_Verification_System.Infrastructure.Storage
{
    public interface IStorageService
    {
        void EnsureRootStructure();

        // Paths
        string GetExtractFilePath(string originalFileName);
        string GetExtractEmailPath(string originalFileName); // same name but .eml
        string GetNoticeBatchFolder(string batchName);
        string GetNoticePdfPath(string batchName, string baseName); // baseName => ObjectionNo or ValuationKey
        string GetNoticeEmailPath(string batchName, string baseName); // .eml
        string GetStatsFilePath(string statsFileName);

        // Save helpers
        Task<string> SaveExtractAsync(IFormFile file, CancellationToken ct = default);
        Task<string> SaveBytesAsync(byte[] bytes, string fullPath, CancellationToken ct = default);
        Task<string> CopyFileAsync(string sourcePath, string destinationFullPath, bool overwrite = true, CancellationToken ct = default);

        // Read helpers (optional)
        bool FileExists(string fullPath);
        Stream OpenRead(string fullPath);

     

        // ✅ Stats
        string GetStatsFolderPath();
        Task<string> SaveStatsAsync(string fileName, byte[] bytes, CancellationToken ct);
       
    }
}
