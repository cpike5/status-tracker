using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StatusTracker.Data;
using StatusTracker.Infrastructure;

namespace StatusTracker.Services;

public sealed class DataRetentionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DataRetentionOptions _options;
    private readonly ILogger<DataRetentionService> _logger;
    private const int BatchSize = 10_000;

    public DataRetentionService(
        IServiceScopeFactory scopeFactory,
        IOptions<DataRetentionOptions> options,
        ILogger<DataRetentionService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.RetentionDays <= 0)
        {
            _logger.LogInformation("Data retention is disabled (RetentionDays = {RetentionDays})", _options.RetentionDays);
            return;
        }

        var (hour, minute) = ParseDailySchedule(_options.PruneSchedule);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var nextRun = GetNextRunTime(now, hour, minute);
            var delay = nextRun - now;

            _logger.LogInformation("Data retention next run scheduled at {NextRun:u}", nextRun);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await PruneAsync(stoppingToken);
        }
    }

    private async Task PruneAsync(CancellationToken ct)
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-_options.RetentionDays);
            var totalDeleted = 0;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            int deleted;
            do
            {
                deleted = await db.CheckResults
                    .Where(r => r.Timestamp < cutoff)
                    .OrderBy(r => r.Timestamp)
                    .Take(BatchSize)
                    .ExecuteDeleteAsync(ct);
                totalDeleted += deleted;
            } while (deleted > 0 && !ct.IsCancellationRequested);

            _logger.LogInformation(
                "Data retention pruned {TotalDeleted} records older than {CutoffDate:u}",
                totalDeleted,
                cutoff);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Data retention pruning failed");
        }
    }

    /// <summary>
    /// Parses hour and minute from a standard daily cron expression ("M H * * *").
    /// Returns (2, 0) as a safe default if parsing fails.
    /// </summary>
    internal static (int hour, int minute) ParseDailySchedule(string cronExpression)
    {
        var parts = cronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2
            && int.TryParse(parts[0], out var minute) && minute is >= 0 and <= 59
            && int.TryParse(parts[1], out var hour)   && hour   is >= 0 and <= 23)
        {
            return (hour, minute);
        }
        return (2, 0); // Default: 02:00 UTC
    }

    /// <summary>
    /// Returns the next UTC datetime at which the daily job should run.
    /// If today's scheduled time is still in the future it returns today's run; otherwise tomorrow's.
    /// </summary>
    internal static DateTime GetNextRunTime(DateTime now, int hour, int minute)
    {
        var today = now.Date.AddHours(hour).AddMinutes(minute);
        return today > now ? today : today.AddDays(1);
    }
}
