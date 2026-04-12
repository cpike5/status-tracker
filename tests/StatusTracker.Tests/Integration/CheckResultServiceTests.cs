using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using StatusTracker.Entities;
using StatusTracker.Services;
using StatusTracker.Validators;

namespace StatusTracker.Tests.Integration;

/// <summary>
/// Integration tests for CheckResultService: recording results, querying recent history,
/// and computing uptime percentages against a real PostgreSQL database.
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class CheckResultServiceTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private readonly IValidator<MonitoredEndpoint> _validator = new MonitoredEndpointValidator();

    public CheckResultServiceTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync() => await _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // ── Helpers ──────────────────────────────────────────────────────────────

    private CheckResultService CreateCheckService(StatusTracker.Data.ApplicationDbContext context) =>
        new(context, NullLogger<CheckResultService>.Instance);

    private EndpointService CreateEndpointService(StatusTracker.Data.ApplicationDbContext context) =>
        new(context, _validator, NullLogger<EndpointService>.Instance);

    private static MonitoredEndpoint BuildValidEndpoint(string name = "Test API") =>
        new()
        {
            Name = name,
            Url = "https://example.com",
            CheckIntervalSeconds = 60,
            ExpectedStatusCode = 200,
            TimeoutSeconds = 10,
            RetryCount = 2,
            IsEnabled = true
        };

    /// <summary>Creates an endpoint and returns its persisted ID.</summary>
    private async Task<int> CreateEndpointAsync(string name = "Test API")
    {
        await using var context = _fixture.CreateDbContext();
        var service = CreateEndpointService(context);
        var endpoint = await service.CreateAsync(BuildValidEndpoint(name));
        return endpoint.Id;
    }

    // ── RecordResultAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task RecordResultAsync_WithValidResult_PersistsToDatabase()
    {
        var endpointId = await CreateEndpointAsync("Record Test");

        await using var context = _fixture.CreateDbContext();
        var service = CreateCheckService(context);

        var result = new CheckResult
        {
            EndpointId = endpointId,
            IsHealthy = true,
            HttpStatusCode = 200,
            ResponseTimeMs = 120,
            Timestamp = DateTime.UtcNow
        };

        await service.RecordResultAsync(result);

        result.Id.Should().BeGreaterThan(0);

        await using var verify = _fixture.CreateDbContext();
        var persisted = await verify.CheckResults.FindAsync(result.Id);
        persisted.Should().NotBeNull();
        persisted!.EndpointId.Should().Be(endpointId);
        persisted.IsHealthy.Should().BeTrue();
        persisted.ResponseTimeMs.Should().Be(120);
    }

    [Fact]
    public async Task RecordResultAsync_WithDefaultTimestamp_SetsTimestampToNow()
    {
        var endpointId = await CreateEndpointAsync("Timestamp Test");

        await using var context = _fixture.CreateDbContext();
        var service = CreateCheckService(context);

        var before = DateTime.UtcNow.AddSeconds(-1);

        // Leave Timestamp at default (DateTime.MinValue) — service should set it
        var result = new CheckResult
        {
            EndpointId = endpointId,
            IsHealthy = true
        };

        await service.RecordResultAsync(result);

        var after = DateTime.UtcNow.AddSeconds(1);

        result.Timestamp.Should().BeAfter(before).And.BeBefore(after);
    }

    [Fact]
    public async Task RecordResultAsync_WithUnhealthyResult_PersistsErrorMessage()
    {
        var endpointId = await CreateEndpointAsync("Error Test");

        await using var context = _fixture.CreateDbContext();
        var service = CreateCheckService(context);

        var result = new CheckResult
        {
            EndpointId = endpointId,
            IsHealthy = false,
            ErrorMessage = "Connection refused",
            Timestamp = DateTime.UtcNow
        };

        await service.RecordResultAsync(result);

        await using var verify = _fixture.CreateDbContext();
        var persisted = await verify.CheckResults.FindAsync(result.Id);
        persisted!.IsHealthy.Should().BeFalse();
        persisted.ErrorMessage.Should().Be("Connection refused");
    }

    // ── GetLatestByEndpointAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetLatestByEndpointAsync_ReturnsResultsInDescendingTimestampOrder()
    {
        var endpointId = await CreateEndpointAsync("Latest Order Test");

        await using var context = _fixture.CreateDbContext();
        var service = CreateCheckService(context);

        var now = DateTime.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            await service.RecordResultAsync(new CheckResult
            {
                EndpointId = endpointId,
                IsHealthy = true,
                Timestamp = now.AddMinutes(-i)
            });
        }

        var results = await service.GetLatestByEndpointAsync(endpointId, 5);

        results.Should().HaveCount(5);
        results.Should().BeInDescendingOrder(r => r.Timestamp);
    }

    [Fact]
    public async Task GetLatestByEndpointAsync_RespectsCountLimit()
    {
        var endpointId = await CreateEndpointAsync("Count Limit Test");

        await using var context = _fixture.CreateDbContext();
        var service = CreateCheckService(context);

        var now = DateTime.UtcNow;
        for (var i = 0; i < 10; i++)
        {
            await service.RecordResultAsync(new CheckResult
            {
                EndpointId = endpointId,
                IsHealthy = true,
                Timestamp = now.AddMinutes(-i)
            });
        }

        var results = await service.GetLatestByEndpointAsync(endpointId, 3);

        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetLatestByEndpointAsync_WithNoResults_ReturnsEmptyList()
    {
        var endpointId = await CreateEndpointAsync("No Results Test");

        await using var context = _fixture.CreateDbContext();
        var service = CreateCheckService(context);

        var results = await service.GetLatestByEndpointAsync(endpointId, 10);

        results.Should().BeEmpty();
    }

    // ── GetUptimePercentageAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetUptimePercentageAsync_With100PercentHealthy_Returns100()
    {
        var endpointId = await CreateEndpointAsync("Full Uptime");

        await using var context = _fixture.CreateDbContext();
        var service = CreateCheckService(context);

        var now = DateTime.UtcNow;
        for (var i = 0; i < 10; i++)
        {
            await service.RecordResultAsync(new CheckResult
            {
                EndpointId = endpointId,
                IsHealthy = true,
                Timestamp = now.AddMinutes(-i)
            });
        }

        var uptime = await service.GetUptimePercentageAsync(endpointId, TimeSpan.FromHours(1));

        uptime.Should().Be(100.00m);
    }

    [Fact]
    public async Task GetUptimePercentageAsync_With0PercentHealthy_Returns0()
    {
        var endpointId = await CreateEndpointAsync("Zero Uptime");

        await using var context = _fixture.CreateDbContext();
        var service = CreateCheckService(context);

        var now = DateTime.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            await service.RecordResultAsync(new CheckResult
            {
                EndpointId = endpointId,
                IsHealthy = false,
                Timestamp = now.AddMinutes(-i)
            });
        }

        var uptime = await service.GetUptimePercentageAsync(endpointId, TimeSpan.FromHours(1));

        uptime.Should().Be(0.00m);
    }

    [Fact]
    public async Task GetUptimePercentageAsync_With80PercentHealthy_Returns80()
    {
        var endpointId = await CreateEndpointAsync("Partial Uptime");

        await using var context = _fixture.CreateDbContext();
        var service = CreateCheckService(context);

        var now = DateTime.UtcNow;

        // 8 healthy
        for (var i = 0; i < 8; i++)
        {
            await service.RecordResultAsync(new CheckResult
            {
                EndpointId = endpointId,
                IsHealthy = true,
                Timestamp = now.AddMinutes(-i)
            });
        }

        // 2 unhealthy
        for (var i = 8; i < 10; i++)
        {
            await service.RecordResultAsync(new CheckResult
            {
                EndpointId = endpointId,
                IsHealthy = false,
                Timestamp = now.AddMinutes(-i)
            });
        }

        var uptime = await service.GetUptimePercentageAsync(endpointId, TimeSpan.FromHours(1));

        uptime.Should().Be(80.00m);
    }

    [Fact]
    public async Task GetUptimePercentageAsync_WithNoResultsInWindow_ReturnsNull()
    {
        var endpointId = await CreateEndpointAsync("No Results Window");

        await using var context = _fixture.CreateDbContext();
        var service = CreateCheckService(context);

        var uptime = await service.GetUptimePercentageAsync(endpointId, TimeSpan.FromHours(1));

        uptime.Should().BeNull();
    }

    [Fact]
    public async Task GetUptimePercentageAsync_ExcludesResultsOutsideWindow()
    {
        var endpointId = await CreateEndpointAsync("Window Boundary");

        await using var context = _fixture.CreateDbContext();
        var service = CreateCheckService(context);

        var now = DateTime.UtcNow;

        // 5 recent healthy checks within the window
        for (var i = 0; i < 5; i++)
        {
            await service.RecordResultAsync(new CheckResult
            {
                EndpointId = endpointId,
                IsHealthy = true,
                Timestamp = now.AddMinutes(-i)
            });
        }

        // 5 old unhealthy checks outside the window
        for (var i = 0; i < 5; i++)
        {
            await service.RecordResultAsync(new CheckResult
            {
                EndpointId = endpointId,
                IsHealthy = false,
                Timestamp = now.AddHours(-2).AddMinutes(-i)
            });
        }

        // 1-hour window should only see the 5 healthy checks
        var uptime = await service.GetUptimePercentageAsync(endpointId, TimeSpan.FromHours(1));

        uptime.Should().Be(100.00m);
    }

    // ── GetResponseTimeHistoryAsync ──────────────────────────────────────────

    [Fact]
    public async Task GetResponseTimeHistoryAsync_ReturnsOnlyResultsWithResponseTime()
    {
        var endpointId = await CreateEndpointAsync("Response Time History");

        await using var context = _fixture.CreateDbContext();
        var service = CreateCheckService(context);

        var now = DateTime.UtcNow;

        await service.RecordResultAsync(new CheckResult
        {
            EndpointId = endpointId,
            IsHealthy = true,
            ResponseTimeMs = 100,
            Timestamp = now.AddMinutes(-2)
        });

        // This one has no response time (e.g. a connection error) — should be excluded
        await service.RecordResultAsync(new CheckResult
        {
            EndpointId = endpointId,
            IsHealthy = false,
            ResponseTimeMs = null,
            Timestamp = now.AddMinutes(-1)
        });

        var history = await service.GetResponseTimeHistoryAsync(endpointId, TimeSpan.FromHours(1));

        history.Should().HaveCount(1);
        history[0].ResponseTimeMs.Should().Be(100);
    }

    [Fact]
    public async Task GetResponseTimeHistoryAsync_ReturnsResultsInAscendingTimestampOrder()
    {
        var endpointId = await CreateEndpointAsync("Response Time Order");

        await using var context = _fixture.CreateDbContext();
        var service = CreateCheckService(context);

        var now = DateTime.UtcNow;
        for (var i = 4; i >= 0; i--)
        {
            await service.RecordResultAsync(new CheckResult
            {
                EndpointId = endpointId,
                IsHealthy = true,
                ResponseTimeMs = (i + 1) * 50,
                Timestamp = now.AddMinutes(-i)
            });
        }

        var history = await service.GetResponseTimeHistoryAsync(endpointId, TimeSpan.FromHours(1));

        history.Should().BeInAscendingOrder(p => p.Timestamp);
    }

    // ── GetDailyUptimeSummaryAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetDailyUptimeSummaryAsync_ReturnsSummaryPerDay()
    {
        var endpointId = await CreateEndpointAsync("Daily Summary");

        await using var context = _fixture.CreateDbContext();
        var service = CreateCheckService(context);

        var today = DateTime.UtcNow.Date;

        // 10 healthy checks today
        for (var i = 0; i < 10; i++)
        {
            await service.RecordResultAsync(new CheckResult
            {
                EndpointId = endpointId,
                IsHealthy = true,
                Timestamp = today.AddHours(i)
            });
        }

        // 5 healthy + 5 unhealthy checks yesterday
        var yesterday = today.AddDays(-1);
        for (var i = 0; i < 5; i++)
        {
            await service.RecordResultAsync(new CheckResult
            {
                EndpointId = endpointId,
                IsHealthy = true,
                Timestamp = yesterday.AddHours(i)
            });
        }
        for (var i = 5; i < 10; i++)
        {
            await service.RecordResultAsync(new CheckResult
            {
                EndpointId = endpointId,
                IsHealthy = false,
                Timestamp = yesterday.AddHours(i)
            });
        }

        var summary = await service.GetDailyUptimeSummaryAsync(endpointId, 2);

        summary.Should().HaveCount(2);

        var todaySummary = summary.First(s => s.Date == DateOnly.FromDateTime(today));
        todaySummary.TotalChecks.Should().Be(10);
        todaySummary.HealthyChecks.Should().Be(10);
        todaySummary.UptimePercent.Should().Be(100.00m);

        var yesterdaySummary = summary.First(s => s.Date == DateOnly.FromDateTime(yesterday));
        yesterdaySummary.TotalChecks.Should().Be(10);
        yesterdaySummary.HealthyChecks.Should().Be(5);
        yesterdaySummary.UptimePercent.Should().Be(50.00m);
    }
}
