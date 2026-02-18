using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Valuer_Verification_System.Services.Notifications;

public sealed class NotificationHostedService : BackgroundService
{
    private readonly IServiceProvider _sp;

    public NotificationHostedService(IServiceProvider sp)
    {
        _sp = sp;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        DateOnly lastRun = default;

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var nowZa = GetNowZa();
            var today = DateOnly.FromDateTime(nowZa);

            if (nowZa.Hour == 8 && nowZa.Minute == 0 && lastRun != today)
            {
                using var scope = _sp.CreateScope();
                var job = scope.ServiceProvider.GetRequiredService<INotificationReminderJob>();
                await job.RunDailyAsync(stoppingToken);
                lastRun = today;
            }
        }
    }

    private static DateTime GetNowZa()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");
        return TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);
    }
}
