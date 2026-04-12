using StatusTracker.Entities;

namespace StatusTracker.Services;

public record ResponseTimePoint(DateTime Timestamp, int ResponseTimeMs);

public record DailyUptimeSummary(DateOnly Date, int TotalChecks, int HealthyChecks, decimal UptimePercent);

public interface ICheckResultService
{
    Task RecordResultAsync(CheckResult result);
    Task<List<CheckResult>> GetLatestByEndpointAsync(int endpointId, int count);
    Task<decimal?> GetUptimePercentageAsync(int endpointId, TimeSpan window);
    Task<List<ResponseTimePoint>> GetResponseTimeHistoryAsync(int endpointId, TimeSpan window);
    Task<List<DailyUptimeSummary>> GetDailyUptimeSummaryAsync(int endpointId, int days);
}
