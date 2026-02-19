using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;

namespace Valuer_Verification_System.Services.Manager
{
    public interface ISectorManagerDirectory
    {
        Task<List<string>> GetAllActiveManagerEmailsAsync(CancellationToken ct);
    }

    public sealed class SectorManagerDirectory : ISectorManagerDirectory
    {
        private readonly VvsDbContext _db;
        public SectorManagerDirectory(VvsDbContext db) => _db = db;

        public Task<List<string>> GetAllActiveManagerEmailsAsync(CancellationToken ct)
        {
            return _db.SectorManagers
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Sector)
                .ThenBy(x => x.FullName)
                .Select(x => x.Email)
                .ToListAsync(ct);
        }
    }
}
