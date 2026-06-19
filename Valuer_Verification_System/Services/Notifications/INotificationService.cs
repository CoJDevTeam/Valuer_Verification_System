using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Models.Data;

namespace Valuer_Verification_System.Services.Notifications;

public interface INotificationService
{
    Task<long> TryCreateAsync(Notification n, CancellationToken ct);
    Task<int> GetUnreadCountForValuerAsync(string valuerKey, CancellationToken ct);
    Task<List<Notification>> GetLatestForValuerAsync(string valuerKey, int top, CancellationToken ct);
    Task MarkReadAsync(long id, string valuerKey, CancellationToken ct);
    Task MarkAllReadAsync(string valuerKey, CancellationToken ct);
}

public sealed class NotificationService : INotificationService
{
    private readonly VvsDbContext _db;

    public NotificationService(VvsDbContext db)
    {
        _db = db;
    }

    public async Task<long> TryCreateAsync(Notification n, CancellationToken ct)
    {
        n.TargetKey = (n.TargetKey ?? "").Trim();
        n.DedupKey = (n.DedupKey ?? "").Trim();

        if (string.IsNullOrWhiteSpace(n.TargetKey))
            return 0;

        _db.Notifications.Add(n);

        try
        {
            await _db.SaveChangesAsync(ct);
            return n.Id;
        }
        catch (DbUpdateException)
        {
            // Duplicate based on unique index -> ignore.
            return 0;
        }
    }

    public Task<int> GetUnreadCountForValuerAsync(string valuerKey, CancellationToken ct)
    {
        valuerKey = (valuerKey ?? "").Trim();

        return _db.Notifications
            .AsNoTracking()
            .CountAsync(x =>
                x.Audience == NotificationAudience.Valuer &&
                x.TargetKey == valuerKey &&
                !x.IsRead,
                ct);
    }

    public Task<List<Notification>> GetLatestForValuerAsync(string valuerKey, int top, CancellationToken ct)
    {
        valuerKey = (valuerKey ?? "").Trim();

        return _db.Notifications
            .AsNoTracking()
            .Where(x =>
                x.Audience == NotificationAudience.Valuer &&
                x.TargetKey == valuerKey)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(top)
            .ToListAsync(ct);
    }

    public async Task MarkReadAsync(long id, string valuerKey, CancellationToken ct)
    {
        valuerKey = (valuerKey ?? "").Trim();

        var n = await _db.Notifications.FirstOrDefaultAsync(x =>
            x.Id == id &&
            x.Audience == NotificationAudience.Valuer &&
            x.TargetKey == valuerKey,
            ct);

        if (n is null || n.IsRead)
            return;

        n.IsRead = true;
        n.ReadAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkAllReadAsync(string valuerKey, CancellationToken ct)
    {
        valuerKey = (valuerKey ?? "").Trim();

        var notes = await _db.Notifications
            .Where(x =>
                x.Audience == NotificationAudience.Valuer &&
                x.TargetKey == valuerKey &&
                !x.IsRead)
            .ToListAsync(ct);

        if (notes.Count == 0)
            return;

        foreach (var n in notes)
        {
            n.IsRead = true;
            n.ReadAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }
}