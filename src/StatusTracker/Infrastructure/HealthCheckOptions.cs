namespace StatusTracker.Infrastructure;

public class HealthCheckOptions
{
    public const string SectionName = "HealthCheck";

    public int DefaultIntervalSeconds { get; set; } = 60;
    public int DefaultTimeoutSeconds { get; set; } = 10;
    public int DefaultRetryCount { get; set; } = 2;
    public int MaxConcurrency { get; set; } = 10;
}
