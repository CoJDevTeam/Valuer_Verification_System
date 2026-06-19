using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Valuer_Verification_System.Services.Notifications;

public sealed class NotificationHostedService : BackgroundService
{
    private static readonly TimeOnly DailyRunAfter = new(8, 0);

    private readonly IServiceProvider _sp;
    private readonly ILogger<NotificationHostedService> _logger;

    public NotificationHostedService(
        IServiceProvider sp,
        ILogger<NotificationHostedService> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notification background service started.");

        DateOnly lastAttemptedRun = default;

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var nowZa = GetNowZa();
                var today = DateOnly.FromDateTime(nowZa);
                var timeNow = TimeOnly.FromDateTime(nowZa);

                // Run once per South African day, any time after 08:00.
                // This avoids missing the job when IIS/app starts at 08:01 or later.
                if (timeNow < DailyRunAfter || lastAttemptedRun == today)
                    continue;

                lastAttemptedRun = today;

                try
                {
                    using var scope = _sp.CreateScope();

                    var job = scope.ServiceProvider
                        .GetRequiredService<INotificationReminderJob>();

                    _logger.LogInformation(
                        "Running daily notification reminder job for {Date}.",
                        today);

                    await job.RunDailyAsync(stoppingToken);

                    _logger.LogInformation(
                        "Daily notification reminder job completed for {Date}.",
                        today);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Daily notification reminder job failed for {Date}.",
                        today);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }

        _logger.LogInformation("Notification background service stopped.");
    }

    private static DateTime GetNowZa()
    {
        TimeZoneInfo tz;

        try
        {
            // Windows IIS/server timezone id
            tz = TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            // Linux timezone id, useful if later hosted in container/Linux
            tz = TimeZoneInfo.FindSystemTimeZoneById("Africa/Johannesburg");
        }

        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
    }
}