namespace Valuer_Verification_System.Services.Upload
{
    public interface IExtractFolderImportLock
    {
        Task<ExtractFolderImportLockHandle?> TryAcquireAsync(CancellationToken ct);
        bool IsRunning { get; }
        DateTime? StartedAtUtc { get; }
        string? StartedBy { get; }
    }

    public sealed class ExtractFolderImportLock : IExtractFolderImportLock
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private DateTime? _startedAtUtc;
        private string? _startedBy;

        public bool IsRunning => _semaphore.CurrentCount == 0;
        public DateTime? StartedAtUtc => _startedAtUtc;
        public string? StartedBy => _startedBy;

        public async Task<ExtractFolderImportLockHandle?> TryAcquireAsync(CancellationToken ct)
        {
            var acquired = await _semaphore.WaitAsync(0, ct);

            if (!acquired)
                return null;

            _startedAtUtc = DateTime.UtcNow;
            _startedBy = Environment.MachineName;

            return new ExtractFolderImportLockHandle(
                _semaphore,
                () =>
                {
                    _startedAtUtc = null;
                    _startedBy = null;
                });
        }
    }

    public sealed class ExtractFolderImportLockHandle : IAsyncDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly Action _onRelease;
        private bool _released;

        public ExtractFolderImportLockHandle(
            SemaphoreSlim semaphore,
            Action onRelease)
        {
            _semaphore = semaphore;
            _onRelease = onRelease;
        }

        public ValueTask DisposeAsync()
        {
            if (_released)
                return ValueTask.CompletedTask;

            _released = true;
            _onRelease();
            _semaphore.Release();

            return ValueTask.CompletedTask;
        }
    }
}