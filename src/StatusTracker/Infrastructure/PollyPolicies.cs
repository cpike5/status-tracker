using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace StatusTracker.Infrastructure;

public static class PollyPolicies
{
    public static ResiliencePipeline<HttpResponseMessage> BuildHealthCheckPipeline(
        int retryCount, int timeoutSeconds, ILogger logger, string endpointName)
    {
        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = retryCount,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
                    .Handle<TimeoutRejectedException>(),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "Health check retry {AttemptNumber}/{MaxRetries} for endpoint {EndpointName}",
                        args.AttemptNumber + 1,
                        retryCount,
                        endpointName);
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(timeoutSeconds),
                OnTimeout = args =>
                {
                    logger.LogWarning(
                        "Health check timed out after {TimeoutSeconds}s for endpoint {EndpointName}",
                        timeoutSeconds,
                        endpointName);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }
}
