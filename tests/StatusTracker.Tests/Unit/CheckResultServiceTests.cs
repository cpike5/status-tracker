using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StatusTracker.Data;
using StatusTracker.Entities;
using StatusTracker.Services;

namespace StatusTracker.Tests.Unit;

/// <summary>
/// Unit tests for CheckResultService using an EF Core InMemory database.
/// </summary>
[Trait("Category", "Unit")]
public class CheckResultServiceTests
{
    // ── Helpers ─────────────────────────────────────────────────────────────

    private static ApplicationDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static CheckResultService CreateService(ApplicationDbContext db) =>
        new(db, NullLogger<CheckResultService>.Instance);

    private static CheckResult MakeResult(
        int endpointId,
        bool isHealthy = true,
        int? responseTimeMs = 150,
        DateTime? timestamp = null) => new()
    {
        EndpointId = endpointId,
        IsHealthy = isHealthy,
        ResponseTimeMs = responseTimeMs,
        Timestamp = timestamp ?? DateTime.UtcNow,
    };

    // ── RecordResultAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task RecordResultAsync_NewResult_PersistsToDatabase()
    {
        await using var db = CreateContext(nameof(RecordResultAsync_NewResult_PersistsToDatabase));
        var sut = CreateService(db);
        var result = MakeResult(endpointId: 1);

        await sut.RecordResultAsync(result);

        db.CheckResults.Should().ContainSingle();
    }

    [Fact]
    public async Task RecordResultAsync_DefaultTimestamp_SetsTimestampToNow()
    {
        await using var db = CreateContext(nameof(RecordResultAsync_DefaultTimestamp_SetsTimestampToNow));
        var sut = CreateService(db);
        var result = new CheckResult
        {
            EndpointId = 1,
            IsHealthy = true,
            // Timestamp is left at default (DateTime.MinValue / default)
        };

        var before = DateTime.UtcNow;
        await sut.RecordResultAsync(result);
        var after = DateTime.UtcNow;

        result.Timestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task RecordResultAsync_ExplicitTimestamp_PreservesProvidedTimestamp()
    {
        await using var db = CreateContext(nameof(RecordResultAsync_ExplicitTimestamp_PreservesProvidedTimestamp));
        var sut = CreateService(db);
        var explicitTime = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var result = MakeResult(endpointId: 1, timestamp: explicitTime);

        await sut.RecordResultAsync(result);

        result.Timestamp.Should().Be(explicitTime);
    }

    // ── GetLatestByEndpointAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetLatestByEndpointAsync_ReturnsResultsOrderedByTimestampDescending()
    {
        await using var db = CreateContext(nameof(GetLatestByEndpointAsync_ReturnsResultsOrderedByTimestampDescending));
        var now = DateTime.UtcNow;
        db.CheckResults.AddRange(
            MakeResult(1, timestamp: now.AddMinutes(-10)),
            MakeResult(1, timestamp: now.AddMinutes(-5)),
            MakeResult(1, timestamp: now.AddMinutes(-1)));
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var results = await sut.GetLatestByEndpointAsync(endpointId: 1, count: 10);

        results.Should().BeInDescendingOrder(r => r.Timestamp);
    }

    [Fact]
    public async Task GetLatestByEndpointAsync_RespectsCount()
    {
        await using var db = CreateContext(nameof(GetLatestByEndpointAsync_RespectsCount));
        var now = DateTime.UtcNow;
        for (var i = 1; i <= 5; i++)
            db.CheckResults.Add(MakeResult(1, timestamp: now.AddMinutes(-i)));
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var results = await sut.GetLatestByEndpointAsync(endpointId: 1, count: 3);

        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetLatestByEndpointAsync_OnlyReturnsResultsForSpecifiedEndpoint()
    {
        await using var db = CreateContext(nameof(GetLatestByEndpointAsync_OnlyReturnsResultsForSpecifiedEndpoint));
        db.CheckResults.AddRange(
            MakeResult(endpointId: 1),
            MakeResult(endpointId: 2));
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var results = await sut.GetLatestByEndpointAsync(endpointId: 1, count: 10);

        results.Should().AllSatisfy(r => r.EndpointId.Should().Be(1));
        results.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetLatestByEndpointAsync_NoResults_ReturnsEmptyList()
    {
        await using var db = CreateContext(nameof(GetLatestByEndpointAsync_NoResults_ReturnsEmptyList));
        var sut = CreateService(db);

        var results = await sut.GetLatestByEndpointAsync(endpointId: 99, count: 10);

        results.Should().BeEmpty();
    }

    // ── GetUptimePercentageAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetUptimePercentageAsync_AllHealthy_Returns100()
    {
        await using var db = CreateContext(nameof(GetUptimePercentageAsync_AllHealthy_Returns100));
        var now = DateTime.UtcNow;
        db.CheckResults.AddRange(
            MakeResult(1, isHealthy: true, timestamp: now.AddHours(-1)),
            MakeResult(1, isHealthy: true, timestamp: now.AddHours(-2)));
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var result = await sut.GetUptimePercentageAsync(endpointId: 1, window: TimeSpan.FromHours(24));

        result.Should().Be(100m);
    }

    [Fact]
    public async Task GetUptimePercentageAsync_AllUnhealthy_Returns0()
    {
        await using var db = CreateContext(nameof(GetUptimePercentageAsync_AllUnhealthy_Returns0));
        var now = DateTime.UtcNow;
        db.CheckResults.AddRange(
            MakeResult(1, isHealthy: false, timestamp: now.AddHours(-1)),
            MakeResult(1, isHealthy: false, timestamp: now.AddHours(-2)));
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var result = await sut.GetUptimePercentageAsync(endpointId: 1, window: TimeSpan.FromHours(24));

        result.Should().Be(0m);
    }

    [Fact]
    public async Task GetUptimePercentageAsync_MixedResults_ReturnsCorrectPercentage()
    {
        // 3 healthy out of 4 total = 75.00%
        await using var db = CreateContext(nameof(GetUptimePercentageAsync_MixedResults_ReturnsCorrectPercentage));
        var now = DateTime.UtcNow;
        db.CheckResults.AddRange(
            MakeResult(1, isHealthy: true, timestamp: now.AddHours(-1)),
            MakeResult(1, isHealthy: true, timestamp: now.AddHours(-2)),
            MakeResult(1, isHealthy: true, timestamp: now.AddHours(-3)),
            MakeResult(1, isHealthy: false, timestamp: now.AddHours(-4)));
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var result = await sut.GetUptimePercentageAsync(endpointId: 1, window: TimeSpan.FromHours(24));

        result.Should().Be(75.00m);
    }

    [Fact]
    public async Task GetUptimePercentageAsync_NoResultsInWindow_ReturnsNull()
    {
        await using var db = CreateContext(nameof(GetUptimePercentageAsync_NoResultsInWindow_ReturnsNull));
        // Seed a result outside the window
        db.CheckResults.Add(MakeResult(1, timestamp: DateTime.UtcNow.AddDays(-10)));
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var result = await sut.GetUptimePercentageAsync(endpointId: 1, window: TimeSpan.FromHours(1));

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUptimePercentageAsync_NoResultsAtAll_ReturnsNull()
    {
        await using var db = CreateContext(nameof(GetUptimePercentageAsync_NoResultsAtAll_ReturnsNull));
        var sut = CreateService(db);

        var result = await sut.GetUptimePercentageAsync(endpointId: 1, window: TimeSpan.FromHours(24));

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUptimePercentageAsync_OnlyCountsResultsWithinWindow()
    {
        // 1 healthy within window, 1 unhealthy outside window → 100%
        await using var db = CreateContext(nameof(GetUptimePercentageAsync_OnlyCountsResultsWithinWindow));
        var now = DateTime.UtcNow;
        db.CheckResults.AddRange(
            MakeResult(1, isHealthy: true, timestamp: now.AddHours(-1)),   // inside 24h window
            MakeResult(1, isHealthy: false, timestamp: now.AddDays(-2)));   // outside 24h window
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var result = await sut.GetUptimePercentageAsync(endpointId: 1, window: TimeSpan.FromHours(24));

        result.Should().Be(100m);
    }

    // ── GetResponseTimeHistoryAsync ──────────────────────────────────────────

    [Fact]
    public async Task GetResponseTimeHistoryAsync_ReturnsPointsOrderedByTimestamp()
    {
        await using var db = CreateContext(nameof(GetResponseTimeHistoryAsync_ReturnsPointsOrderedByTimestamp));
        var now = DateTime.UtcNow;
        db.CheckResults.AddRange(
            MakeResult(1, responseTimeMs: 300, timestamp: now.AddHours(-3)),
            MakeResult(1, responseTimeMs: 100, timestamp: now.AddHours(-1)),
            MakeResult(1, responseTimeMs: 200, timestamp: now.AddHours(-2)));
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var points = await sut.GetResponseTimeHistoryAsync(endpointId: 1, window: TimeSpan.FromHours(24));

        points.Should().BeInAscendingOrder(p => p.Timestamp);
        points.Select(p => p.ResponseTimeMs).Should().Equal(300, 200, 100);
    }

    [Fact]
    public async Task GetResponseTimeHistoryAsync_ExcludesResultsWithNullResponseTime()
    {
        await using var db = CreateContext(nameof(GetResponseTimeHistoryAsync_ExcludesResultsWithNullResponseTime));
        var now = DateTime.UtcNow;
        db.CheckResults.AddRange(
            MakeResult(1, responseTimeMs: 100, timestamp: now.AddHours(-1)),
            MakeResult(1, responseTimeMs: null, timestamp: now.AddHours(-2)));  // should be excluded
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var points = await sut.GetResponseTimeHistoryAsync(endpointId: 1, window: TimeSpan.FromHours(24));

        points.Should().HaveCount(1);
        points[0].ResponseTimeMs.Should().Be(100);
    }

    [Fact]
    public async Task GetResponseTimeHistoryAsync_NoResultsInWindow_ReturnsEmptyList()
    {
        await using var db = CreateContext(nameof(GetResponseTimeHistoryAsync_NoResultsInWindow_ReturnsEmptyList));
        var sut = CreateService(db);

        var points = await sut.GetResponseTimeHistoryAsync(endpointId: 1, window: TimeSpan.FromHours(1));

        points.Should().BeEmpty();
    }

    // ── GetDailyUptimeSummaryAsync ───────────────────────────────────────────

    [Fact]
    public async Task GetDailyUptimeSummaryAsync_GroupsByDate()
    {
        await using var db = CreateContext(nameof(GetDailyUptimeSummaryAsync_GroupsByDate));
        var today = DateTime.UtcNow.Date;
        var yesterday = today.AddDays(-1);

        db.CheckResults.AddRange(
            MakeResult(1, isHealthy: true, timestamp: today.AddHours(1)),
            MakeResult(1, isHealthy: false, timestamp: yesterday.AddHours(1)));
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var summary = await sut.GetDailyUptimeSummaryAsync(endpointId: 1, days: 7);

        summary.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetDailyUptimeSummaryAsync_CalculatesCorrectUptimePercent()
    {
        // 2 healthy + 2 unhealthy on a single day = 50%
        await using var db = CreateContext(nameof(GetDailyUptimeSummaryAsync_CalculatesCorrectUptimePercent));
        var today = DateTime.UtcNow.Date;

        db.CheckResults.AddRange(
            MakeResult(1, isHealthy: true, timestamp: today.AddHours(1)),
            MakeResult(1, isHealthy: true, timestamp: today.AddHours(2)),
            MakeResult(1, isHealthy: false, timestamp: today.AddHours(3)),
            MakeResult(1, isHealthy: false, timestamp: today.AddHours(4)));
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var summary = await sut.GetDailyUptimeSummaryAsync(endpointId: 1, days: 1);

        var todaySummary = summary.Should().ContainSingle().Subject;
        todaySummary.UptimePercent.Should().Be(50.00m);
        todaySummary.TotalChecks.Should().Be(4);
        todaySummary.HealthyChecks.Should().Be(2);
    }

    [Fact]
    public async Task GetDailyUptimeSummaryAsync_ResultsOrderedByDateAscending()
    {
        await using var db = CreateContext(nameof(GetDailyUptimeSummaryAsync_ResultsOrderedByDateAscending));
        var today = DateTime.UtcNow.Date;

        db.CheckResults.AddRange(
            MakeResult(1, timestamp: today.AddDays(-2).AddHours(1)),
            MakeResult(1, timestamp: today.AddDays(-1).AddHours(1)),
            MakeResult(1, timestamp: today.AddHours(1)));
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var summary = await sut.GetDailyUptimeSummaryAsync(endpointId: 1, days: 7);

        summary.Should().BeInAscendingOrder(s => s.Date);
    }

    [Fact]
    public async Task GetDailyUptimeSummaryAsync_ExcludesResultsOlderThanWindow()
    {
        await using var db = CreateContext(nameof(GetDailyUptimeSummaryAsync_ExcludesResultsOlderThanWindow));
        var today = DateTime.UtcNow.Date;

        db.CheckResults.AddRange(
            MakeResult(1, timestamp: today.AddHours(1)),           // within 1-day window
            MakeResult(1, timestamp: today.AddDays(-5).AddHours(1))); // outside 1-day window
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var summary = await sut.GetDailyUptimeSummaryAsync(endpointId: 1, days: 1);

        summary.Should().HaveCount(1);
        summary[0].Date.Should().Be(DateOnly.FromDateTime(today));
    }

    [Fact]
    public async Task GetDailyUptimeSummaryAsync_NoResults_ReturnsEmptyList()
    {
        await using var db = CreateContext(nameof(GetDailyUptimeSummaryAsync_NoResults_ReturnsEmptyList));
        var sut = CreateService(db);

        var summary = await sut.GetDailyUptimeSummaryAsync(endpointId: 1, days: 7);

        summary.Should().BeEmpty();
    }
}
