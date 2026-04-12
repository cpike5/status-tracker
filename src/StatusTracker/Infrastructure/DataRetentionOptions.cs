namespace StatusTracker.Infrastructure;

public class DataRetentionOptions
{
    public const string SectionName = "DataRetention";

    public int RetentionDays { get; set; } = 90;
    public string PruneSchedule { get; set; } = "0 2 * * *"; // 2 AM daily
}
