using Microsoft.EntityFrameworkCore;
using Valuer_Verification_System.Data;
using Valuer_Verification_System.Models.Data;
using Valuer_Verification_System.Models.SLA;


namespace Valuer_Verification_System.Services.Notifications;

public interface INotificationReminderJob
{
    Task RunDailyAsync(CancellationToken ct);
}

public sealed class NotificationReminderJob : INotificationReminderJob
{
    private readonly VvsDbContext _db;
    private readonly INotificationService _notif;

    public NotificationReminderJob(VvsDbContext db, INotificationService notif)
    {
        _db = db;
        _notif = notif;
    }

    public async Task RunDailyAsync(CancellationToken ct)
    {
        var todayZa = SlaCalculator.GetTodayZa();
        var dayKey = todayZa.ToString("yyyyMMdd");

        // scan recent batches only
        var fromUtc = DateTime.UtcNow.AddDays(-30);
        var batchIds = await _db.Batches.AsNoTracking()
            .Where(b => b.CreatedAt >= fromUtc)
            .OrderByDescending(b => b.Id)
            .Take(30)
            .Select(b => b.Id)
            .ToListAsync(ct);

        if (batchIds.Count == 0) return;

        var pendingByValuer = await _db.Assignments.AsNoTracking()
            .Where(a => batchIds.Contains(a.ExtractRow.BatchId)
                     && a.ExtractRow.Decision == VerificationDecision.Pending)
            .GroupBy(a => a.AssignedToValuer)
            .Select(g => new { Valuer = g.Key, Pending = g.Count() })
            .Where(x => x.Pending > 0 && x.Valuer != null && x.Valuer != "")
            .ToListAsync(ct);

        foreach (var v in pendingByValuer)
        {
            await _notif.TryCreateAsync(new Notification
            {
                Audience = NotificationAudience.Valuer,
                TargetKey = v.Valuer!.Trim(),
                Type = NotificationType.ReminderPending,
                DedupKey = $"ValuerPending_{dayKey}",
                Title = "Pending verifications reminder",
                Message = $"You have {v.Pending} pending verification(s). Please complete them.",
                ActionUrl = "/Valuer/Inbox?filter=Pending"
            }, ct);
        }
    }
}
