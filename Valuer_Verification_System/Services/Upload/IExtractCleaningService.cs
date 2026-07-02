namespace Valuer_Verification_System.Services.Upload
{
    public interface IExtractCleaningService
    {
        Task CleanSectorValuerAndResolveUsernameAsync(int batchId, string updatedBy, CancellationToken ct);
    }
}
