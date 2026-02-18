using Microsoft.Extensions.Options;
using Valuer_Verification_System.Infrastructure.Configuration;

namespace Valuer_Verification_System.Infrastructure.Storage
{
    public sealed class StorageService : IStorageService
    {
        private readonly StorageSettings _s;

        public StorageService(IOptions<StorageSettings> opt)
        {
            _s = opt.Value;

            if (string.IsNullOrWhiteSpace(_s.RootPath))
                throw new InvalidOperationException("Storage:RootPath is required.");
        }

        public void EnsureRootStructure()
        {
            Directory.CreateDirectory(_s.RootPath);
            Directory.CreateDirectory(_s.ExtractsPath);
            Directory.CreateDirectory(_s.NoticesPath);
            Directory.CreateDirectory(_s.StatsPath);
        }

        public string GetExtractFilePath(string originalFileName)
        {
            var safeName = SafeFileName(originalFileName);
            return EnsureUnderRoot(Path.Combine(_s.ExtractsPath, safeName));
        }

        public string GetExtractEmailPath(string originalFileName)
        {
            var safeName = SafeFileName(originalFileName);
            var emlName = Path.ChangeExtension(safeName, ".eml");
            return EnsureUnderRoot(Path.Combine(_s.ExtractsPath, emlName));
        }

        public string GetNoticeBatchFolder(string batchName)
        {
            var safe = SafeFolderName(batchName);
            return EnsureUnderRoot(Path.Combine(_s.NoticesPath, safe));
        }

        public string GetNoticePdfPath(string batchName, string baseName)
        {
            var folder = GetNoticeBatchFolder(batchName);
            var safeBase = SafeBaseName(baseName);
            return EnsureUnderRoot(Path.Combine(folder, safeBase + ".pdf"));
        }

        public string GetNoticeEmailPath(string batchName, string baseName)
        {
            var folder = GetNoticeBatchFolder(batchName);
            var safeBase = SafeBaseName(baseName);
            return EnsureUnderRoot(Path.Combine(folder, safeBase + ".eml"));
        }

        public string GetStatsFilePath(string statsFileName)
        {
            var safeName = SafeFileName(statsFileName);
            if (!safeName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                safeName = Path.ChangeExtension(safeName, ".xlsx");

            return EnsureUnderRoot(Path.Combine(_s.StatsPath, safeName));
        }


        public async Task<string> SaveExtractAsync(IFormFile file, CancellationToken ct = default)
        {
            if (file == null || file.Length <= 0)
                throw new ArgumentException("Upload file is empty.", nameof(file));

            EnsureRootStructure();

            // Keep the original filename (but sanitised to prevent path injection)
            var fullPath = GetExtractFilePath(file.FileName);

            var dir = Path.GetDirectoryName(fullPath)!;
            Directory.CreateDirectory(dir);

            await using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await file.CopyToAsync(fs, ct);

            return fullPath;
        }

        public async Task<string> SaveBytesAsync(byte[] bytes, string fullPath, CancellationToken ct = default)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));

            EnsureRootStructure();

            fullPath = EnsureUnderRoot(fullPath);

            var dir = Path.GetDirectoryName(fullPath)!;
            Directory.CreateDirectory(dir);

            await File.WriteAllBytesAsync(fullPath, bytes, ct);
            return fullPath;
        }

        public async Task<string> CopyFileAsync(string sourcePath, string destinationFullPath, bool overwrite = true, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                throw new ArgumentException("Source path required.", nameof(sourcePath));

            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("Source file not found.", sourcePath);

            EnsureRootStructure();

            destinationFullPath = EnsureUnderRoot(destinationFullPath);

            var dir = Path.GetDirectoryName(destinationFullPath)!;
            Directory.CreateDirectory(dir);

            // No async copy built-in; use streams
            var mode = overwrite ? FileMode.Create : FileMode.CreateNew;

            await using var src = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            await using var dst = new FileStream(destinationFullPath, mode, FileAccess.Write, FileShare.None);
            await src.CopyToAsync(dst, 81920, ct);

            return destinationFullPath;
        }

        public bool FileExists(string fullPath)
        {
            fullPath = EnsureUnderRoot(fullPath);
            return File.Exists(fullPath);
        }

        public Stream OpenRead(string fullPath)
        {
            fullPath = EnsureUnderRoot(fullPath);
            return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        // --------------------------
        // Safety helpers
        // --------------------------

        private string EnsureUnderRoot(string path)
        {
            // Convert to absolute and ensure it stays under RootPath
            var root = Path.GetFullPath(_s.RootPath.Trim());
            var full = Path.GetFullPath(path);

            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Invalid storage path (outside RootPath).");

            return full;
        }

        private static string SafeFileName(string fileName)
        {
            // Remove any path segments and invalid characters
            var name = Path.GetFileName(fileName);

            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            // Avoid blank names
            if (string.IsNullOrWhiteSpace(name))
                name = "file";

            return name;
        }

        private static string SafeFolderName(string folderName)
        {
            // No slashes, no traversal
            var name = folderName.Trim();

            name = name.Replace("\\", "_").Replace("/", "_").Replace("..", "_");

            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            if (string.IsNullOrWhiteSpace(name))
                name = "folder";

            return name;
        }

        private static string SafeBaseName(string baseName)
        {
            // For ObjectionNo/ValuationKey etc. (no extension)
            var name = baseName.Trim();

            name = name.Replace("\\", "_").Replace("/", "_").Replace("..", "_");

            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            // extra safety: remove trailing dots/spaces (windows issues)
            name = name.Trim().TrimEnd('.', ' ');

            if (string.IsNullOrWhiteSpace(name))
                name = "item";

            return name;
        }

        public string GetStatsFolderPath()
        {
            EnsureRootStructure();
            return EnsureUnderRoot(_s.StatsPath);
        }
        public async Task<string> SaveStatsAsync(string fileName, byte[] bytes, CancellationToken ct = default)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));

            EnsureRootStructure();

            var fullPath = GetStatsFilePath(fileName);

            var dir = Path.GetDirectoryName(fullPath)!;
            Directory.CreateDirectory(dir);

            await File.WriteAllBytesAsync(fullPath, bytes, ct);
            return fullPath;
        }
    }
}