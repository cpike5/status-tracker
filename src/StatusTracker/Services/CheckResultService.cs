using Microsoft.EntityFrameworkCore;
using StatusTracker.Data;
using StatusTracker.Entities;

namespace StatusTracker.Services;

public class CheckResultService : ICheckResultService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<CheckResultService> _logger;

    public CheckResultService(ApplicationDbContext db, ILogger<CheckResultService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RecordResultAsync(CheckResult result)
    {
        if (result.Timestamp == default)
        {
            result.Timestamp = DateTime.UtcNow;
        }

        _db.CheckResults.Add(result);
        await _db.SaveChangesAsync();

        _logger.LogDebug(
            "Recorded check result for endpoint {EndpointId}: IsHealthy={IsHealthy}, ResponseTimeMs={ResponseTimeMs}",
            result.EndpointId,
            result.IsHealthy,
            result.ResponseTimeMs);
    }

    public async Task<List<CheckResult>> GetLatestByEndpointAsync(int endpointId, int count)
    {
        return await _db.CheckResults
            .Where(r => r.EndpointId == endpointId)
            .OrderByDescending(r => r.Timestamp)
            .Take(count)
            .ToListAsync();
    }

    public async Task<decimal?> GetUptimePercentageAsync(int endpointId, TimeSpan window)
    {
        var cutoff = DateTime.UtcNow - window;

        var counts = await _db.CheckResults
            .Where(r => r.EndpointId == endpointId && r.Timestamp >= cutoff)
            .GroupBy(_ => 1)
            .Select(g => new { Total = g.Count(), Healthy = g.Count(r => r.IsHealthy) })
            .FirstOrDefaultAsync();

        if (counts is null || counts.Total == 0)
            return null;

        return Math.Round((decimal)counts.Healthy / counts.Total * 100m, 2);
    }

    public async Task<List<ResponseTimePoint>> GetResponseTimeHistoryAsync(int endpointId, TimeSpan window)
    {
        var cutoff = DateTime.UtcNow - window;

        return await _db.CheckResults
            .Where(r => r.EndpointId == endpointId
                        && r.Timestamp >= cutoff
                        && r.ResponseTimeMs != null)
            .OrderBy(r => r.Timestamp)
            .Select(r => new ResponseTimePoint(r.Timestamp, r.ResponseTimeMs!.Value))
            .ToListAsync();
    }

    public async Task<List<DailyUptimeSummary>> GetDailyUptimeSummaryAsync(int endpointId, int days)
    {
        var cutoff = DateTime.UtcNow.Date.AddDays(-(days - 1));

        var dailyData = await _db.CheckResults
            .Where(r => r.EndpointId == endpointId && r.Timestamp >= cutoff)
            .GroupBy(r => r.Timestamp.Date)
            .Select(g => new
            {
                Date = g.Key,
                Total = g.Count(),
                Healthy = g.Count(r => r.IsHealthy)
            })
            .OrderBy(g => g.Date)
            .ToListAsync();

        return dailyData
            .Select(d => new DailyUptimeSummary(
                DateOnly.FromDateTime(d.Date),
                d.Total,
                d.Healthy,
                d.Total > 0 ? Math.Round((decimal)d.Healthy / d.Total * 100m, 2) : 0m))
            .ToList();
    }
}
