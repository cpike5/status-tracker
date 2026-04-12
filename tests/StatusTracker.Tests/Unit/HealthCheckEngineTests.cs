using System.Net;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StatusTracker.Entities;
using StatusTracker.Infrastructure;
using StatusTracker.Services;

namespace StatusTracker.Tests.Unit;

/// <summary>
/// Unit tests for HealthCheckEngine.
///
/// CheckEndpointAsync and RunTickAsync are private, so tests invoke RunTickAsync
/// via reflection to keep them fast and deterministic (no real PeriodicTimer).
///
/// The scope factory chain is built by BuildScopeFactory so each test provides
/// its own IEndpointService and ICheckResultService fakes in isolation.
///
/// Polly notes:
///   - MaxRetryAttempts must be >= 1 (Polly v8 validation), so MakeEndpoint
///     defaults to retryCount = 1.
///   - Exception-path tests throw InvalidOperationException, which is NOT in
///     Polly's ShouldHandle list, so the exception propagates immediately without
///     a retry delay — keeping those tests fast.
/// </summary>
[Trait("Category", "Unit")]
public class HealthCheckEngineTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the IServiceScopeFactory → IServiceScope → IServiceProvider chain
    /// that HealthCheckEngine uses for both endpoint loading and result recording.
    /// </summary>
    private static IServiceScopeFactory BuildScopeFactory(
        IEndpointService endpointService,
        ICheckResultService checkResultService)
    {
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(IEndpointService)).Returns(endpointService);
        provider.GetService(typeof(ICheckResultService)).Returns(checkResultService);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(provider);

        var factory = Substitute.For<IServiceScopeFactory>();
        factory.CreateScope().Returns(scope);

        return factory;
    }

    /// <summary>
    /// Creates a MockHttpMessageHandler that returns the given status code and body.
    /// </summary>
    private static MockHttpMessageHandler HandlerFor(HttpStatusCode statusCode, string body = "")
        => new(statusCode, body);

    /// <summary>
    /// Builds a HealthCheckEngine wired to the given endpoint service and HTTP
    /// handler. Returns the engine and the ICheckResultService mock so tests can
    /// assert on recorded results.
    /// </summary>
    private static (HealthCheckEngine Engine, ICheckResultService ResultService) BuildEngine(
        IEndpointService endpointService,
        HttpMessageHandler httpHandler,
        IStatusUpdateNotifier? notifier = null)
    {
        var resultService = Substitute.For<ICheckResultService>();
        var scopeFactory = BuildScopeFactory(endpointService, resultService);

        var httpClient = new HttpClient(httpHandler);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("HealthCheck").Returns(httpClient);

        var options = Options.Create(new HealthCheckOptions { MaxConcurrency = 4 });
        var resolvedNotifier = notifier ?? Substitute.For<IStatusUpdateNotifier>();

        var engine = new HealthCheckEngine(
            scopeFactory,
            httpClientFactory,
            options,
            NullLogger<HealthCheckEngine>.Instance,
            resolvedNotifier);

        return (engine, resultService);
    }

    /// <summary>
    /// Creates a MonitoredEndpoint with sensible defaults for test use.
    /// retryCount defaults to 1 because Polly v8 requires MaxRetryAttempts >= 1.
    /// </summary>
    private static MonitoredEndpoint MakeEndpoint(
        int id = 1,
        string url = "http://example.com/health",
        int expectedStatusCode = 200,
        string? expectedBodyMatch = null,
        int checkIntervalSeconds = 60,
        int retryCount = 1,
        int timeoutSeconds = 5)
        => new()
        {
            Id = id,
            Name = $"Endpoint-{id}",
            Url = url,
            ExpectedStatusCode = expectedStatusCode,
            ExpectedBodyMatch = expectedBodyMatch,
            CheckIntervalSeconds = checkIntervalSeconds,
            RetryCount = retryCount,
            TimeoutSeconds = timeoutSeconds,
            IsEnabled = true,
        };

    /// <summary>
    /// Invokes RunTickAsync via reflection. Pre-seeds _nextCheckTimes so all
    /// supplied endpoints are treated as immediately due for a check.
    /// </summary>
    private static async Task RunTickAsync(HealthCheckEngine engine, IEnumerable<MonitoredEndpoint> endpoints)
    {
        var nextCheckTimesField = typeof(HealthCheckEngine)
            .GetField("_nextCheckTimes", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var dict = (System.Collections.Concurrent.ConcurrentDictionary<int, DateTime>)
            nextCheckTimesField.GetValue(engine)!;

        foreach (var ep in endpoints)
            dict[ep.Id] = DateTime.UtcNow.AddSeconds(-1); // mark as past-due

        var method = typeof(HealthCheckEngine)
            .GetMethod("RunTickAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        await (Task)method.Invoke(engine, [CancellationToken.None])!;
    }

    // ── Successful health check ───────────────────────────────────────────────

    [Fact]
    public async Task CheckEndpointAsync_ExpectedStatusCodeReturned_RecordsHealthyResult()
    {
        // Arrange
        var endpoint = MakeEndpoint(expectedStatusCode: 200);
        var endpointService = Substitute.For<IEndpointService>();
        endpointService.GetEnabledAsync().Returns([endpoint]);

        var (engine, resultService) = BuildEngine(endpointService, HandlerFor(HttpStatusCode.OK));

        // Act
        await RunTickAsync(engine, [endpoint]);

        // Assert
        await resultService.Received(1).RecordResultAsync(
            Arg.Is<CheckResult>(r =>
                r.EndpointId == endpoint.Id &&
                r.IsHealthy == true &&
                r.HttpStatusCode == 200 &&
                r.ErrorMessage == null));
    }

    [Fact]
    public async Task CheckEndpointAsync_ExpectedStatusCodeReturned_ResponseTimeMsIsPopulated()
    {
        // Arrange
        var endpoint = MakeEndpoint(expectedStatusCode: 200);
        var endpointService = Substitute.For<IEndpointService>();
        endpointService.GetEnabledAsync().Returns([endpoint]);

        CheckResult? captured = null;
        var resultService = Substitute.For<ICheckResultService>();
        resultService
            .RecordResultAsync(Arg.Do<CheckResult>(r => captured = r))
            .Returns(Task.CompletedTask);

        var scopeFactory = BuildScopeFactory(endpointService, resultService);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("HealthCheck")
            .Returns(new HttpClient(HandlerFor(HttpStatusCode.OK)));

        var engine = new HealthCheckEngine(
            scopeFactory,
            httpClientFactory,
            Options.Create(new HealthCheckOptions { MaxConcurrency = 4 }),
            NullLogger<HealthCheckEngine>.Instance,
            Substitute.For<IStatusUpdateNotifier>());

        // Act
        await RunTickAsync(engine, [endpoint]);

        // Assert
        captured.Should().NotBeNull();
        captured!.ResponseTimeMs.Should().BeGreaterThanOrEqualTo(0);
    }

    // ── Failed status code ────────────────────────────────────────────────────

    [Fact]
    public async Task CheckEndpointAsync_UnexpectedStatusCode_RecordsUnhealthyResult()
    {
        // Endpoint expects 200, server returns 503.
        var endpoint = MakeEndpoint(expectedStatusCode: 200);
        var endpointService = Substitute.For<IEndpointService>();
        endpointService.GetEnabledAsync().Returns([endpoint]);

        var (engine, resultService) = BuildEngine(endpointService, HandlerFor(HttpStatusCode.ServiceUnavailable));

        // Act
        await RunTickAsync(engine, [endpoint]);

        // Assert
        await resultService.Received(1).RecordResultAsync(
            Arg.Is<CheckResult>(r =>
                r.EndpointId == endpoint.Id &&
                r.IsHealthy == false &&
                r.HttpStatusCode == 503));
    }

    [Fact]
    public async Task CheckEndpointAsync_UnexpectedStatusCode_ResponseTimeMsIsPopulated()
    {
        // A bad status code is not an exception; response time is still measured.
        var endpoint = MakeEndpoint(expectedStatusCode: 200);
        var endpointService = Substitute.For<IEndpointService>();
        endpointService.GetEnabledAsync().Returns([endpoint]);

        CheckResult? captured = null;
        var resultService = Substitute.For<ICheckResultService>();
        resultService
            .RecordResultAsync(Arg.Do<CheckResult>(r => captured = r))
            .Returns(Task.CompletedTask);

        var scopeFactory = BuildScopeFactory(endpointService, resultService);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("HealthCheck")
            .Returns(new HttpClient(HandlerFor(HttpStatusCode.NotFound)));

        var engine = new HealthCheckEngine(
            scopeFactory,
            httpClientFactory,
            Options.Create(new HealthCheckOptions { MaxConcurrency = 4 }),
            NullLogger<HealthCheckEngine>.Instance,
            Substitute.For<IStatusUpdateNotifier>());

        await RunTickAsync(engine, [endpoint]);

        captured!.ResponseTimeMs.Should().BeGreaterThanOrEqualTo(0);
        captured.ErrorMessage.Should().BeNull();
    }

    // ── Body match: plain text ────────────────────────────────────────────────

    [Fact]
    public async Task CheckEndpointAsync_BodyMatchConfiguredAndBodyContainsText_RecordsHealthyResult()
    {
        // Arrange
        var endpoint = MakeEndpoint(expectedStatusCode: 200, expectedBodyMatch: "healthy");
        var endpointService = Substitute.For<IEndpointService>();
        endpointService.GetEnabledAsync().Returns([endpoint]);

        var (engine, resultService) = BuildEngine(
            endpointService, HandlerFor(HttpStatusCode.OK, "Service is healthy and running"));

        // Act
        await RunTickAsync(engine, [endpoint]);

        // Assert
        await resultService.Received(1).RecordResultAsync(
            Arg.Is<CheckResult>(r => r.EndpointId == endpoint.Id && r.IsHealthy == true));
    }

    [Fact]
    public async Task CheckEndpointAsync_BodyMatchConfiguredAndBodyDoesNotContainText_RecordsUnhealthyResult()
    {
        // Arrange
        var endpoint = MakeEndpoint(expectedStatusCode: 200, expectedBodyMatch: "healthy");
        var endpointService = Substitute.For<IEndpointService>();
        endpointService.GetEnabledAsync().Returns([endpoint]);

        var (engine, resultService) = BuildEngine(
            endpointService, HandlerFor(HttpStatusCode.OK, "Service is degraded"));

        // Act
        await RunTickAsync(engine, [endpoint]);

        // Assert
        await resultService.Received(1).RecordResultAsync(
            Arg.Is<CheckResult>(r => r.EndpointId == endpoint.Id && r.IsHealthy == false));
    }

    [Fact]
    public async Task CheckEndpointAsync_BodyMatchIsCaseInsensitive_RecordsHealthyResult()
    {
        // EvaluateBodyMatch uses StringComparison.OrdinalIgnoreCase for plain text.
        var endpoint = MakeEndpoint(expectedStatusCode: 200, expectedBodyMatch: "HEALTHY");
        var endpointService = Substitute.For<IEndpointService>();
        endpointService.GetEnabledAsync().Returns([endpoint]);

        var (engine, resultService) = BuildEngine(
            endpointService, HandlerFor(HttpStatusCode.OK, "service is healthy"));

        await RunTickAsync(engine, [endpoint]);

        await resultService.Received(1).RecordResultAsync(
            Arg.Is<CheckResult>(r => r.EndpointId == endpoint.Id && r.IsHealthy == true));
    }

    [Fact]
    public async Task CheckEndpointAsync_BodyMatchNotConfigured_HealthDeterminedByStatusCodeOnly()
    {
        // When ExpectedBodyMatch is null, only the status code matters.
        var endpoint = MakeEndpoint(expectedStatusCode: 200, expectedBodyMatch: null);
        var endpointService = Substitute.For<IEndpointService>();
        endpointService.GetEnabledAsync().Returns([endpoint]);

        var (engine, resultService) = BuildEngine(
            endpointService, HandlerFor(HttpStatusCode.OK, "arbitrary body content"));

        await RunTickAsync(engine, [endpoint]);

        await resultService.Received(1).RecordResultAsync(
            Arg.Is<CheckResult>(r => r.EndpointId == endpoint.Id && r.IsHealthy == true));
    }

    [Fact]
    public async Task CheckEndpointAsync_StatusCodeUnhealthyWithBodyMatchSet_ReturnsUnhealthyWithoutReadingBody()
    {
        // Body match is only evaluated when the status code check passes first
        // (guarded by `if (isHealthy && ...)`). A 503 response should be unhealthy
        // even if the body would have matched.
        var endpoint = MakeEndpoint(expectedStatusCode: 200, expectedBodyMatch: "healthy");
        var endpointService = Substitute.For<IEndpointService>();
        endpointService.GetEnabledAsync().Returns([endpoint]);

        var (engine, resultService) = BuildEngine(
            endpointService, HandlerFor(HttpStatusCode.ServiceUnavailable, "healthy"));

        await RunTickAsync(engine, [endpoint]);

        await resultService.Received(1).RecordResultAsync(
            Arg.Is<CheckResult>(r => r.EndpointId == endpoint.Id && r.IsHealthy == false));
    }

    // ── Body match: regex ─────────────────────────────────────────────────────

    [Fact]
    public async Task CheckEndpointAsync_RegexBodyMatchAndBodyMatches_RecordsHealthyResult()
    {
        // A pattern delimited by slashes is treated as a regex by EvaluateBodyMatch.
        var endpoint = MakeEndpoint(expectedStatusCode: 200, expectedBodyMatch: "/ok|healthy/");
        var endpointService = Substitute.For<IEndpointService>();
        endpointService.GetEnabledAsync().Returns([endpoint]);

        var (engine, resultService) = BuildEngine(
            endpointService, HandlerFor(HttpStatusCode.OK, "status: ok"));

        await RunTickAsync(engine, [endpoint]);

        await resultService.Received(1).RecordResultAsync(
            Arg.Is<CheckResult>(r => r.EndpointId == endpoint.Id && r.IsHealthy == true));
    }

    [Fact]
    public async Task CheckEndpointAsync_RegexBodyMatchAndBodyDoesNotMatch_RecordsUnhealthyResult()
    {
        var endpoint = MakeEndpoint(expectedStatusCode: 200, expectedBodyMatch: "/^healthy$/");
        var endpointService = Substitute.For<IEndpointService>();
        endpointService.GetEnabledAsync().Returns([endpoint]);

        var (engine, resultService) = BuildEngine(
            endpointService, HandlerFor(HttpStatusCode.OK, "degraded"));

        await RunTickAsync(engine, [endpoint]);

        await resultService.Received(1).RecordResultAsync(
            Arg.Is<CheckResult>(r => r.EndpointId == endpoint.Id && r.IsHealthy == false));
    }

    [Fact]
    public async Task CheckEndpointAsync_RegexBodyMatchWithQuantifier_EvaluatesCorrectly()
    {
        // Regex with \d+ should match a numeric response time pattern.
        var endpoint = MakeEndpoint(
            expectedStatusCode: 200,
            expectedBodyMatch: @"/response_time:\s*\d+ms/");
        var endpointService = Substitute.For<IEndpointService>();
        endpointService.GetEnabledAsync().Returns([endpoint]);

        var (engine, resultService) = BuildEngine(
            endpointService, HandlerFor(HttpStatusCode.OK, "response_time: 142ms"));

        await RunTickAsync(engine, [endpoint]);

        await resultService.Received(1).RecordResultAsync(
            Arg.Is<CheckResult>(r => r.EndpointId == endpoint.Id && r.IsHealthy == true));
    }

    // ── HTTP exception ────────────────────────────────────────────────────────
    //
    // Exception tests use InvalidOperationException which is NOT in Polly's
    // ShouldHandle list, so the pipeline propagates it immediately without
    // a retry delay. This keeps the tests fast while still exercising the
    // engine's catch block behavior.

    [Fact]
    public async Task CheckEndpointAsync_HttpClientThrows_RecordsUnhealthyResultWithNullResponseTime()
    {
        // Arrange
        var endpoint = MakeEndpoint();
        var endpointService = Substitute.For<IEndpointService>();
        endpointService.GetEnabledAsync().Returns([endpoint]);

        var throwingHandler = new ThrowingHttpMessageHandler(
            new InvalidOperationException("Connection refused"));

        var (engine, resultService) = BuildEngine(endpointService, throwingHandler);

        // Act
        await RunTickAsync(engine, [endpoint]);

        // Assert — exception must NOT propagate; engine records a failed result.
        await resultService.Received(1).RecordResultAsync(
            Arg.Is<CheckResult>(r =>
                r.EndpointId == endpoint.Id &&
                r.IsHealthy == false &&
                r.ResponseTimeMs == null &&
                r.HttpStatusCode == null));
    }

    [Fact]
    public async Task CheckEndpointAsync_HttpClientThrows_ErrorMessageContainsExceptionMessage()
    {
        var endpoint = MakeEndpoint();
        var endpointService = Substitute.For<IEndpointService>();
        endpointService.GetEnabledAsync().Returns([endpoint]);

        const string exceptionMessage = "Connection refused";
        var throwingHandler = new ThrowingHttpMessageHandler(
            new InvalidOperationException(exceptionMessage));

        CheckResult? captured = null;
        var resultService = Substitute.For<ICheckResultService>();
        resultService
            .RecordResultAsync(Arg.Do<CheckResult>(r => captured = r))
            .Returns(Task.CompletedTask);

        var scopeFactory = BuildScopeFactory(endpointService, resultService);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("HealthCheck")
            .Returns(new HttpClient(throwingHandler));

        var engine = new HealthCheckEngine(
            scopeFactory,
            httpClientFactory,
            Options.Create(new HealthCheckOptions { MaxConcurrency = 4 }),
            NullLogger<HealthCheckEngine>.Instance,
            Substitute.For<IStatusUpdateNotifier>());

        await RunTickAsync(engine, [endpoint]);

        captured!.ErrorMessage.Should().Contain(exceptionMessage);
    }

    [Fact]
    public async Task CheckEndpointAsync_HttpClientThrows_DoesNotRethrowException()
    {
        // The engine must swallow all exceptions from CheckEndpointAsync.
        var endpoint = MakeEndpoint();
        var endpointService = Substitute.For<IEndpointService>();
        endpointService.GetEnabledAsync().Returns([endpoint]);

        var throwingHandler = new ThrowingHttpMessageHandler(
            new InvalidOperationException("Network error"));

        var (engine, _) = BuildEngine(endpointService, throwingHandler);

        var act = () => RunTickAsync(engine, [endpoint]);

        await act.Should().NotThrowAsync();
    }

    // ── Result recording failure ──────────────────────────────────────────────

    [Fact]
    public async Task CheckEndpointAsync_RecordResultThrowsAfterHttpFailure_DoesNotRethrow()
    {
        // When both the HTTP call and the subsequent RecordResultAsync fail,
        // the engine must swallow both exceptions.
        var endpoint = MakeEndpoint();
        var endpointService = Substitute.For<IEndpointService>();
        endpointService.GetEnabledAsync().Returns([endpoint]);

        var resultService = Substitute.For<ICheckResultService>();
        resultService
            .RecordResultAsync(Arg.Any<CheckResult>())
            .ThrowsAsync(new InvalidOperationException("DB unavailable"));

        var throwingHandler = new ThrowingHttpMessageHandler(
            new InvalidOperationException("Network error"));

        var scopeFactory = BuildScopeFactory(endpointService, resultService);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("HealthCheck")
            .Returns(new HttpClient(throwingHandler));

        var engine = new HealthCheckEngine(
            scopeFactory,
            httpClientFactory,
            Options.Create(new HealthCheckOptions { MaxConcurrency = 4 }),
            NullLogger<HealthCheckEngine>.Instance,
            Substitute.For<IStatusUpdateNotifier>());

        var act = () => RunTickAsync(engine, [endpoint]);

        await act.Should().NotThrowAsync();
    }

    // ── Next check time advancement ───────────────────────────────────────────

    [Fact]
    public async Task CheckEndpointAsync_AfterSuccessfulCheck_AdvancesNextCheckTimeByInterval()
    {
        // _nextCheckTimes[id] should be set to approximately now + CheckIntervalSeconds.
        var endpoint = MakeEndpoint(checkIntervalSeconds: 30);
        var endpointService = Substitute.For<IEndpointService>();
        endpointService.GetEnabledAsync().Returns([endpoint]);

        var (engine, _) = BuildEngine(endpointService, HandlerFor(HttpStatusCode.OK));

        var before = DateTime.UtcNow;
        await RunTickAsync(engine, [endpoint]);
        var after = DateTime.UtcNow;

        var nextCheckTimesField = typeof(HealthCheckEngine)
            .GetField("_nextCheckTimes", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var dict = (System.Collections.Concurrent.ConcurrentDictionary<int, DateTime>)
            nextCheckTimesField.GetValue(engine)!;

        dict.Should().ContainKey(endpoint.Id);
        var nextCheck = dict[endpoint.Id];
        nextCheck.Should().BeOnOrAfter(before.AddSeconds(30));
        nextCheck.Should().BeOnOrBefore(after.AddSeconds(30));
    }

    [Fact]
    public async Task CheckEndpointAsync_AfterHttpException_StillAdvancesNextCheckTime()
    {
        // The finally block in CheckEndpointAsync must advance the next-check time
        // even when an exception is thrown.
        var endpoint = MakeEndpoint(checkIntervalSeconds: 60);
        var endpointService = Substitute.For<IEndpointService>();
        endpointService.GetEnabledAsync().Returns([endpoint]);

        var throwingHandler = new ThrowingHttpMessageHandler(
            new InvalidOperationException("Network error"));

        var (engine, _) = BuildEngine(endpointService, throwingHandler);

        await RunTickAsync(engine, [endpoint]);

        var nextCheckTimesField = typeof(HealthCheckEngine)
            .GetField("_nextCheckTimes", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var dict = (System.Collections.Concurrent.ConcurrentDictionary<int, DateTime>)
            nextCheckTimesField.GetValue(engine)!;

        dict.Should().ContainKey(endpoint.Id);
        dict[endpoint.Id].Should().BeAfter(DateTime.UtcNow.AddSeconds(50));
    }

    // ── Notifier ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CheckEndpointAsync_SuccessfulCheck_NotifiesUpdateForEndpoint()
    {
        var endpoint = MakeEndpoint();
        var endpointService = Substitute.For<IEndpointService>();
        endpointService.GetEnabledAsync().Returns([endpoint]);

        var notifier = Substitute.For<IStatusUpdateNotifier>();
        var (engine, _) = BuildEngine(endpointService, HandlerFor(HttpStatusCode.OK), notifier);

        await RunTickAsync(engine, [endpoint]);

        notifier.Received(1).NotifyUpdate(endpoint.Id);
    }

    [Fact]
    public async Task CheckEndpointAsync_HttpExceptionAndRecordSucceeds_NotifiesUpdateForEndpoint()
    {
        // The notifier should still fire after a failed HTTP call if recording succeeds.
        var endpoint = MakeEndpoint();
        var endpointService = Substitute.For<IEndpointService>();
        endpointService.GetEnabledAsync().Returns([endpoint]);

        var notifier = Substitute.For<IStatusUpdateNotifier>();
        var throwingHandler = new ThrowingHttpMessageHandler(
            new InvalidOperationException("Connection refused"));

        var (engine, _) = BuildEngine(endpointService, throwingHandler, notifier);

        await RunTickAsync(engine, [endpoint]);

        notifier.Received(1).NotifyUpdate(endpoint.Id);
    }

    // ── RunTickAsync: endpoint scheduling ─────────────────────────────────────

    [Fact]
    public async Task RunTickAsync_EndpointNotYetDue_SkipsCheck()
    {
        // An endpoint whose next-check time is in the future must not be checked.
        var endpoint = MakeEndpoint();
        var endpointService = Substitute.For<IEndpointService>();
        endpointService.GetEnabledAsync().Returns([endpoint]);

        var (engine, resultService) = BuildEngine(endpointService, HandlerFor(HttpStatusCode.OK));

        // Set the next-check time to the future BEFORE calling RunTickAsync.
        var nextCheckTimesField = typeof(HealthCheckEngine)
            .GetField("_nextCheckTimes", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var dict = (System.Collections.Concurrent.ConcurrentDictionary<int, DateTime>)
            nextCheckTimesField.GetValue(engine)!;
        dict[endpoint.Id] = DateTime.UtcNow.AddSeconds(120);

        var method = typeof(HealthCheckEngine)
            .GetMethod("RunTickAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(engine, [CancellationToken.None])!;

        await resultService.DidNotReceive().RecordResultAsync(Arg.Any<CheckResult>());
    }

    [Fact]
    public async Task RunTickAsync_NoEnabledEndpoints_RecordsNoResults()
    {
        var endpointService = Substitute.For<IEndpointService>();
        endpointService.GetEnabledAsync().Returns([]);

        var (engine, resultService) = BuildEngine(endpointService, HandlerFor(HttpStatusCode.OK));

        var method = typeof(HealthCheckEngine)
            .GetMethod("RunTickAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(engine, [CancellationToken.None])!;

        await resultService.DidNotReceive().RecordResultAsync(Arg.Any<CheckResult>());
    }

    [Fact]
    public async Task RunTickAsync_GetEnabledEndpointsThrows_DoesNotRethrow()
    {
        // If loading endpoints fails, the error is logged and the tick is skipped.
        var endpointService = Substitute.For<IEndpointService>();
        endpointService
            .GetEnabledAsync()
            .ThrowsAsync(new InvalidOperationException("DB connection lost"));

        var (engine, resultService) = BuildEngine(endpointService, HandlerFor(HttpStatusCode.OK));

        var method = typeof(HealthCheckEngine)
            .GetMethod("RunTickAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var act = async () => await (Task)method.Invoke(engine, [CancellationToken.None])!;

        await act.Should().NotThrowAsync();
        await resultService.DidNotReceive().RecordResultAsync(Arg.Any<CheckResult>());
    }

    [Fact]
    public async Task RunTickAsync_MultipleEndpointsDue_ChecksAllEndpoints()
    {
        var endpoints = new[]
        {
            MakeEndpoint(id: 1, url: "http://svc1.example.com/health"),
            MakeEndpoint(id: 2, url: "http://svc2.example.com/health"),
            MakeEndpoint(id: 3, url: "http://svc3.example.com/health"),
        };

        var endpointService = Substitute.For<IEndpointService>();
        endpointService.GetEnabledAsync().Returns([.. endpoints]);

        var (engine, resultService) = BuildEngine(endpointService, HandlerFor(HttpStatusCode.OK));

        await RunTickAsync(engine, endpoints);

        await resultService.Received(3).RecordResultAsync(Arg.Any<CheckResult>());
    }
}

// ── Test doubles ──────────────────────────────────────────────────────────────

/// <summary>
/// An HttpMessageHandler that always returns a fixed HTTP status code and body.
/// </summary>
internal sealed class MockHttpMessageHandler(HttpStatusCode statusCode, string body = "")
    : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body)
        };
        return Task.FromResult(response);
    }
}

/// <summary>
/// An HttpMessageHandler that always throws a given exception, simulating a
/// network-level failure. Use an exception type that is NOT in Polly's
/// ShouldHandle list (e.g., InvalidOperationException) to avoid retry delays
/// in unit tests.
/// </summary>
internal sealed class ThrowingHttpMessageHandler(Exception exception) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => throw exception;
}
