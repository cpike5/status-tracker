namespace StatusTracker.Entities;

public class CheckResult
{
    public long Id { get; set; }
    public int EndpointId { get; set; }
    public DateTime Timestamp { get; set; }
    public int? ResponseTimeMs { get; set; }
    public int? HttpStatusCode { get; set; }
    public bool IsHealthy { get; set; }
    public string? ErrorMessage { get; set; }

    public MonitoredEndpoint Endpoint { get; set; } = null!;
}
