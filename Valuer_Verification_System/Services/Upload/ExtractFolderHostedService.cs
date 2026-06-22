using Microsoft.Extensions.Options;
using Valuer_Verification_System.Infrastructure.Configuration;

namespace Valuer_Verification_System.Services.Upload
{
    public sealed class ExtractFolderHostedService : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly ExtractInboxSettings _settings;
        private readonly ILogger<ExtractFolderHostedService> _logger;

        public ExtractFolderHostedService(
            IServiceProvider sp,
            IOptions<ExtractInboxSettings> settings,
            ILogger<ExtractFolderHostedService> logger)
        {
            _sp = sp;
            _settings = settings.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Extract folder background service started.");

            DateOnly lastAttemptedRun = default;

            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    var nowZa = GetNowZa();
                    var today = DateOnly.FromDateTime(nowZa);

                    if (nowZa.Hour < _settings.RunAfterHour)
                        continue;

                    if (lastAttemptedRun == today)
                        continue;

                    lastAttemptedRun = today;

                    try
                    {
                        using var scope = _sp.CreateScope();

                        var job = scope.ServiceProvider
                            .GetRequiredService<IExtractFolderImportService>();

                        _logger.LogInformation(
                            "Running extract folder import job for {Date}.",
                            today);

                        var result = await job.ImportNewFilesAsync(stoppingToken);

                        _logger.LogInformation(
                            "Extract folder import job completed. Imported: {Imported}, Skipped: {Skipped}, Failed: {Failed}.",
                            result.ImportedCount,
                            result.SkippedCount,
                            result.FailedCount);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Extract folder import job failed for {Date}.",
                            today);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown.
            }

            _logger.LogInformation("Extract folder background service stopped.");
        }

        private static DateTime GetNowZa()
        {
            TimeZoneInfo tz;

            try
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById("Africa/Johannesburg");
            }

            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        }
    }
}