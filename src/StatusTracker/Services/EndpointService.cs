using FluentValidation;
using Microsoft.EntityFrameworkCore;
using StatusTracker.Data;
using StatusTracker.Entities;

namespace StatusTracker.Services;

public class EndpointService : IEndpointService
{
    private const int DegradedResponseTimeMultiplier = 5;
    private const double DegradedUnhealthyThreshold = 0.20;

    private readonly ApplicationDbContext _db;
    private readonly IValidator<MonitoredEndpoint> _validator;
    private readonly ILogger<EndpointService> _logger;

    public EndpointService(
        ApplicationDbContext db,
        IValidator<MonitoredEndpoint> validator,
        ILogger<EndpointService> logger)
    {
        _db = db;
        _validator = validator;
        _logger = logger;
    }

    public async Task<List<MonitoredEndpoint>> GetAllAsync()
    {
        return await _db.MonitoredEndpoints
            .AsNoTracking()
            .OrderBy(e => e.SortOrder)
            .ThenBy(e => e.Name)
            .ToListAsync();
    }

    public async Task<List<MonitoredEndpoint>> GetEnabledAsync()
    {
        return await _db.MonitoredEndpoints
            .AsNoTracking()
            .Where(e => e.IsEnabled)
            .OrderBy(e => e.SortOrder)
            .ThenBy(e => e.Name)
            .ToListAsync();
    }

    public async Task<MonitoredEndpoint?> GetByIdAsync(int id)
    {
        return await _db.MonitoredEndpoints
            .AsNoTracking()
            .Include(e => e.CheckResults
                .OrderByDescending(r => r.Timestamp)
                .Take(100))
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<List<EndpointStatusDto>> GetWithStatusAsync()
    {
        var endpoints = await _db.MonitoredEndpoints
            .AsNoTracking()
            .OrderBy(e => e.SortOrder)
            .ThenBy(e => e.Name)
            .ToListAsync();

        if (endpoints.Count == 0)
            return [];

        var endpointIds = endpoints.Select(e => e.Id).ToList();

        var now = DateTime.UtcNow;
        var window30d = now.AddDays(-30);
        var window7d = now.AddDays(-7);
        var window24h = now.AddHours(-24);
        var window1h = now.AddHours(-1);

        // Pull the single latest check result per endpoint.
        var latestResults = await _db.CheckResults
            .AsNoTracking()
            .Where(r => endpointIds.Contains(r.EndpointId))
            .GroupBy(r => r.EndpointId)
            .Select(g => g.OrderByDescending(r => r.Timestamp).First())
            .ToListAsync();

        // Pull aggregates for the uptime windows and the degraded-check window in one
        // query, grouping by endpoint. We retrieve summary rows rather than raw rows to
        // avoid pulling large volumes of data into memory.
        var aggregates = await _db.CheckResults
            .AsNoTracking()
            .Where(r => endpointIds.Contains(r.EndpointId) && r.Timestamp >= window30d)
            .GroupBy(r => r.EndpointId)
            .Select(g => new
            {
                EndpointId = g.Key,

                // 24-hour window
                Total24h = g.Count(r => r.Timestamp >= window24h),
                Healthy24h = g.Count(r => r.Timestamp >= window24h && r.IsHealthy),

                // 7-day window
                Total7d = g.Count(r => r.Timestamp >= window7d),
                Healthy7d = g.Count(r => r.Timestamp >= window7d && r.IsHealthy),

                // 30-day window
                Total30d = g.Count(),
                Healthy30d = g.Count(r => r.IsHealthy),

                // Data for degraded detection: checks in the last hour
                Total1h = g.Count(r => r.Timestamp >= window1h),
                Unhealthy1h = g.Count(r => r.Timestamp >= window1h && !r.IsHealthy),

                // Average and latest response times within the last hour (for degraded threshold)
                AvgResponseMs1h = g
                    .Where(r => r.Timestamp >= window1h && r.ResponseTimeMs != null)
                    .Average(r => (double?)r.ResponseTimeMs),
            })
            .ToListAsync();

        var latestByEndpoint = latestResults.ToDictionary(r => r.EndpointId);
        var aggregatesByEndpoint = aggregates.ToDictionary(a => a.EndpointId);

        var dtos = new List<EndpointStatusDto>(endpoints.Count);

        foreach (var endpoint in endpoints)
        {
            latestByEndpoint.TryGetValue(endpoint.Id, out var latest);
            aggregatesByEndpoint.TryGetValue(endpoint.Id, out var agg);

            var status = DetermineStatus(latest, agg?.Total1h ?? 0, agg?.Unhealthy1h ?? 0, agg?.AvgResponseMs1h);

            decimal? uptime24h = agg is { Total24h: > 0 }
                ? Math.Round((decimal)agg.Healthy24h / agg.Total24h * 100, 2)
                : null;

            decimal? uptime7d = agg is { Total7d: > 0 }
                ? Math.Round((decimal)agg.Healthy7d / agg.Total7d * 100, 2)
                : null;

            decimal? uptime30d = agg is { Total30d: > 0 }
                ? Math.Round((decimal)agg.Healthy30d / agg.Total30d * 100, 2)
                : null;

            dtos.Add(new EndpointStatusDto
            {
                Endpoint = endpoint,
                Status = status,
                LatestResponseTimeMs = latest?.ResponseTimeMs,
                Uptime24h = uptime24h,
                Uptime7d = uptime7d,
                Uptime30d = uptime30d,
            });
        }

        return dtos;
    }

    public async Task<MonitoredEndpoint> CreateAsync(MonitoredEndpoint endpoint)
    {
        await ValidateOrThrowAsync(endpoint);

        var now = DateTime.UtcNow;
        endpoint.CreatedAt = now;
        endpoint.UpdatedAt = now;

        _db.MonitoredEndpoints.Add(endpoint);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created endpoint {EndpointId} ({EndpointName})", endpoint.Id, endpoint.Name);

        return endpoint;
    }

    public async Task UpdateAsync(MonitoredEndpoint endpoint)
    {
        await ValidateOrThrowAsync(endpoint);

        endpoint.UpdatedAt = DateTime.UtcNow;

        _db.MonitoredEndpoints.Update(endpoint);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Updated endpoint {EndpointId} ({EndpointName})", endpoint.Id, endpoint.Name);
    }

    public async Task DeleteAsync(int id)
    {
        var rows = await _db.MonitoredEndpoints
            .Where(e => e.Id == id)
            .ExecuteDeleteAsync();

        if (rows == 0)
        {
            _logger.LogWarning("DeleteAsync: endpoint {EndpointId} not found", id);
            return;
        }

        _logger.LogInformation("Deleted endpoint {EndpointId}", id);
    }

    public async Task ToggleEnabledAsync(int id)
    {
        var endpoint = await _db.MonitoredEndpoints.FindAsync(id);
        if (endpoint is null)
        {
            _logger.LogWarning("ToggleEnabledAsync: endpoint {EndpointId} not found", id);
            return;
        }

        endpoint.IsEnabled = !endpoint.IsEnabled;
        endpoint.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Endpoint {EndpointId} ({EndpointName}) IsEnabled toggled to {IsEnabled}",
            endpoint.Id, endpoint.Name, endpoint.IsEnabled);
    }

    // --- Private helpers ---

    private static EndpointStatus DetermineStatus(
        CheckResult? latest,
        int total1h,
        int unhealthy1h,
        double? avgResponseMs1h)
    {
        if (latest is null)
            return EndpointStatus.Unknown;

        if (!latest.IsHealthy)
            return EndpointStatus.Down;

        // Degraded: more than 20% of checks in the last hour are unhealthy
        if (total1h > 0 && (double)unhealthy1h / total1h > DegradedUnhealthyThreshold)
            return EndpointStatus.Degraded;

        // Degraded: latest response time exceeds 5× the average over the last hour
        if (latest.ResponseTimeMs.HasValue
            && avgResponseMs1h.HasValue
            && avgResponseMs1h.Value > 0
            && latest.ResponseTimeMs.Value > DegradedResponseTimeMultiplier * avgResponseMs1h.Value)
        {
            return EndpointStatus.Degraded;
        }

        return EndpointStatus.Up;
    }

    private async Task ValidateOrThrowAsync(MonitoredEndpoint endpoint)
    {
        var result = await _validator.ValidateAsync(endpoint);
        if (!result.IsValid)
            throw new ValidationException(result.Errors);
    }
}
