namespace StatusTracker.Entities;

public class MonitoredEndpoint
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Group { get; set; }
    public string Url { get; set; } = string.Empty;
    public int CheckIntervalSeconds { get; set; } = 60;
    public int ExpectedStatusCode { get; set; } = 200;
    public string? ExpectedBodyMatch { get; set; }
    public int TimeoutSeconds { get; set; } = 10;
    public int RetryCount { get; set; } = 2;
    public bool IsEnabled { get; set; } = true;
    public bool IsPublic { get; set; } = false;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CheckResult> CheckResults { get; set; } = [];
}
