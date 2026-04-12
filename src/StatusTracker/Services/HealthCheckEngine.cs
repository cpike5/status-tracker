using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Polly;
using Serilog.Context;
using StatusTracker.Entities;
using StatusTracker.Infrastructure;

namespace StatusTracker.Services;

public sealed class HealthCheckEngine : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HealthCheckOptions _options;
    private readonly ILogger<HealthCheckEngine> _logger;

    private readonly ConcurrentDictionary<int, DateTime> _nextCheckTimes = new();
    private readonly ConcurrentDictionary<(int RetryCount, int TimeoutSeconds), ResiliencePipeline<HttpResponseMessage>> _pipelineCache = new();

    public HealthCheckEngine(
        IServiceScopeFactory serviceScopeFactory,
        IHttpClientFactory httpClientFactory,
        IOptions<HealthCheckOptions> options,
        ILogger<HealthCheckEngine> logger)
    {
        _scopeFactory = serviceScopeFactory;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HealthCheckEngine starting");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunTickAsync(stoppingToken);
        }

        _logger.LogInformation("HealthCheckEngine stopping");
    }

    private async Task RunTickAsync(CancellationToken stoppingToken)
    {
        List<MonitoredEndpoint> enabledEndpoints;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var endpointService = scope.ServiceProvider.GetRequiredService<IEndpointService>();
            enabledEndpoints = await endpointService.GetEnabledAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve enabled endpoints during tick");
            return;
        }

        var now = DateTime.UtcNow;

        // Register newly-discovered endpoints with staggered start times.
        foreach (var endpoint in enabledEndpoints)
        {
            _nextCheckTimes.GetOrAdd(endpoint.Id, _ =>
            {
                var delaySeconds = Random.Shared.NextDouble() * 30;
                return now.AddSeconds(delaySeconds);
            });
        }

        // Select endpoints that are due for a check.
        var due = enabledEndpoints
            .Where(e => _nextCheckTimes.TryGetValue(e.Id, out var next) && next <= now)
            .ToList();

        if (due.Count == 0)
            return;

        await Parallel.ForEachAsync(
            due,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = _options.MaxConcurrency,
                CancellationToken = stoppingToken
            },
            async (endpoint, ct) => await CheckEndpointAsync(endpoint, ct));
    }

    private async Task CheckEndpointAsync(MonitoredEndpoint endpoint, CancellationToken ct)
    {
        using (LogContext.PushProperty("EndpointId", endpoint.Id))
        using (LogContext.PushProperty("EndpointName", endpoint.Name))
        using (LogContext.PushProperty("EndpointUrl", endpoint.Url))
        {
            try
            {
                var pipeline = _pipelineCache.GetOrAdd(
                    (endpoint.RetryCount, endpoint.TimeoutSeconds),
                    key => PollyPolicies.BuildHealthCheckPipeline(key.RetryCount, key.TimeoutSeconds, _logger, endpoint.Name));

                var httpClient = _httpClientFactory.CreateClient("HealthCheck");

                var stopwatch = Stopwatch.StartNew();

                using var response = await pipeline.ExecuteAsync(
                    async innerCt => await httpClient.GetAsync(endpoint.Url, HttpCompletionOption.ResponseHeadersRead, innerCt),
                    ct);
                stopwatch.Stop();

                var responseTimeMs = (int)stopwatch.ElapsedMilliseconds;
                var statusCode = (int)response.StatusCode;
                var isHealthy = statusCode == endpoint.ExpectedStatusCode;

                // Evaluate body match only if configured — read body after timing.
                if (isHealthy && !string.IsNullOrEmpty(endpoint.ExpectedBodyMatch))
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    isHealthy = EvaluateBodyMatch(body, endpoint.ExpectedBodyMatch);
                }

                using (LogContext.PushProperty("CheckDurationMs", responseTimeMs))
                using (LogContext.PushProperty("IsHealthy", isHealthy))
                {
                    var result = new CheckResult
                    {
                        EndpointId = endpoint.Id,
                        Timestamp = DateTime.UtcNow,
                        ResponseTimeMs = responseTimeMs,
                        HttpStatusCode = statusCode,
                        IsHealthy = isHealthy,
                        ErrorMessage = null
                    };

                    await RecordResultAsync(result);

                    _logger.LogInformation(
                        "Health check completed: {EndpointId} {EndpointName} — IsHealthy: {IsHealthy}, ResponseTimeMs: {ResponseTimeMs}",
                        endpoint.Id,
                        endpoint.Name,
                        isHealthy,
                        responseTimeMs);
                }
            }
            catch (Exception ex)
            {
                using (LogContext.PushProperty("CheckDurationMs", (int?)null))
                using (LogContext.PushProperty("IsHealthy", false))
                {
                    _logger.LogError(ex,
                        "Health check failed for {EndpointId} {EndpointName}: {Error}",
                        endpoint.Id,
                        endpoint.Name,
                        ex.Message);

                    var failedResult = new CheckResult
                    {
                        EndpointId = endpoint.Id,
                        Timestamp = DateTime.UtcNow,
                        ResponseTimeMs = null,
                        HttpStatusCode = null,
                        IsHealthy = false,
                        ErrorMessage = ex.Message
                    };

                    try
                    {
                        await RecordResultAsync(failedResult);
                    }
                    catch (Exception recordEx)
                    {
                        _logger.LogError(recordEx,
                            "Failed to record error result for {EndpointId} {EndpointName}",
                            endpoint.Id,
                            endpoint.Name);
                    }
                }
            }
            finally
            {
                // Always advance the next check time so the endpoint is not retried immediately.
                _nextCheckTimes[endpoint.Id] = DateTime.UtcNow.AddSeconds(endpoint.CheckIntervalSeconds);
            }
        }
    }

    private async Task RecordResultAsync(CheckResult result)
    {
        using var scope = _scopeFactory.CreateScope();
        var checkResultService = scope.ServiceProvider.GetRequiredService<ICheckResultService>();
        await checkResultService.RecordResultAsync(result);
    }

    private static bool EvaluateBodyMatch(string body, string pattern)
    {
        if (pattern.StartsWith('/') && pattern.EndsWith('/') && pattern.Length > 2)
        {
            var regex = pattern[1..^1];
            return Regex.IsMatch(body, regex, RegexOptions.None, TimeSpan.FromSeconds(5));
        }
        return body.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }
}
