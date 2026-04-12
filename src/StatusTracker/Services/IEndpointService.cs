using StatusTracker.Entities;

namespace StatusTracker.Services;

public enum EndpointStatus
{
    Up,
    Down,
    Degraded,
    Unknown
}

public class EndpointStatusDto
{
    public MonitoredEndpoint Endpoint { get; init; } = null!;
    public EndpointStatus Status { get; init; }
    public int? LatestResponseTimeMs { get; init; }
    public decimal? Uptime24h { get; init; }
    public decimal? Uptime7d { get; init; }
    public decimal? Uptime30d { get; init; }
}

public interface IEndpointService
{
    Task<List<MonitoredEndpoint>> GetAllAsync();
    Task<List<MonitoredEndpoint>> GetEnabledAsync();
    Task<MonitoredEndpoint?> GetByIdAsync(int id);
    Task<List<EndpointStatusDto>> GetWithStatusAsync();
    Task<MonitoredEndpoint> CreateAsync(MonitoredEndpoint endpoint);
    Task UpdateAsync(MonitoredEndpoint endpoint);
    Task DeleteAsync(int id);
    Task ToggleEnabledAsync(int id);
}
