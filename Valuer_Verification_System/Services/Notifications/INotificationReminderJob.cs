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
    private readonly ILogger<NotificationReminderJob> _logger;

    public NotificationReminderJob(
        VvsDbContext db,
        INotificationService notif,
        ILogger<NotificationReminderJob> logger)
    {
        _db = db;
        _notif = notif;
        _logger = logger;
    }

    public async Task RunDailyAsync(CancellationToken ct)
    {
        var todayZa = SlaCalculator.GetTodayZa();
        var dayKey = todayZa.ToString("yyyyMMdd");

        var fromUtc = DateTime.UtcNow.AddDays(-30);

        var batchIds = await _db.Batches
            .AsNoTracking()
            .Where(b => b.CreatedAt >= fromUtc)
            .OrderByDescending(b => b.Id)
            .Take(30)
            .Select(b => b.Id)
            .ToListAsync(ct);

        if (batchIds.Count == 0)
        {
            _logger.LogInformation("Reminder job skipped. No recent batches found.");
            return;
        }

        var pendingByValuer = await _db.ExtractRows
     .AsNoTracking()
     .Where(r =>
         batchIds.Contains(r.BatchId) &&
         r.Decision == VerificationDecision.Pending &&
         r.Username != null &&
         r.Username != "")
     .GroupBy(r => r.Username!.Trim())
     .Select(g => new
     {
         Valuer = g.Key,
         Pending = g.Count()
     })
     .Where(x => x.Pending > 0 && x.Valuer != "")
     .ToListAsync(ct);

        if (pendingByValuer.Count == 0)
        {
            _logger.LogInformation("Reminder job completed. No pending valuer verifications found.");
            return;
        }

        foreach (var v in pendingByValuer)
        {
            await _notif.TryCreateAsync(new Notification
            {
                Audience = NotificationAudience.Valuer,
                TargetKey = v.Valuer,
                Type = NotificationType.ReminderPending,

                // Important:
                // Include valuer in the dedup key.
                // Otherwise only one valuer can get the daily reminder.
                DedupKey = $"ValuerPending_{v.Valuer}_{dayKey}",

                Title = "Pending verifications reminder",
                Message = $"You have {v.Pending} pending verification(s). Please complete them.",
                ActionUrl = "/Valuer/Inbox?filter=Pending"
            }, ct);
        }

        _logger.LogInformation(
            "Reminder job completed. Created/checked reminders for {ValuerCount} valuer(s).",
            pendingByValuer.Count);
    }
}