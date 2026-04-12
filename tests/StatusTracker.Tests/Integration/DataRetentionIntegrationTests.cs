using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StatusTracker.Entities;
using StatusTracker.Services;
using StatusTracker.Validators;

namespace StatusTracker.Tests.Integration;

/// <summary>
/// Integration tests for the data retention pruning logic against a real PostgreSQL database.
/// Rather than spinning up the full BackgroundService (which sleeps until its schedule),
/// we call the internal pruning query directly via a helper that mirrors PruneAsync.
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class DataRetentionIntegrationTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private readonly IValidator<MonitoredEndpoint> _validator = new MonitoredEndpointValidator();

    public DataRetentionIntegrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync() => await _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // ── Helpers ──────────────────────────────────────────────────────────────

    private EndpointService CreateEndpointService(StatusTracker.Data.ApplicationDbContext context) =>
        new(context, _validator, NullLogger<EndpointService>.Instance);

    private CheckResultService CreateCheckService(StatusTracker.Data.ApplicationDbContext context) =>
        new(context, NullLogger<CheckResultService>.Instance);

    private static MonitoredEndpoint BuildValidEndpoint(string name = "Retention Test") =>
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

    private async Task<int> CreateEndpointAsync(string name = "Retention Test")
    {
        await using var context = _fixture.CreateDbContext();
        var service = CreateEndpointService(context);
        var endpoint = await service.CreateAsync(BuildValidEndpoint(name));
        return endpoint.Id;
    }

    /// <summary>
    /// Runs the same pruning query used by DataRetentionService.PruneAsync,
    /// deleting rows older than <paramref name="cutoff"/> in batches of 10 000.
    /// </summary>
    private async Task<int> PruneOlderThanAsync(DateTime cutoff)
    {
        const int batchSize = 10_000;
        var totalDeleted = 0;

        await using var context = _fixture.CreateDbContext();

        int deleted;
        do
        {
            deleted = await context.CheckResults
                .Where(r => r.Timestamp < cutoff)
                .OrderBy(r => r.Timestamp)
                .Take(batchSize)
                .ExecuteDeleteAsync();
            totalDeleted += deleted;
        } while (deleted > 0);

        return totalDeleted;
    }

    // ── Pruning logic ────────────────────────────────────────────────────────

    [Fact]
    public async Task Prune_DeletesOnlyRecordsOlderThanCutoff()
    {
        var endpointId = await CreateEndpointAsync("Prune Old Only");

        await using var context = _fixture.CreateDbContext();
        var service = CreateCheckService(context);

        var cutoff = DateTime.UtcNow.AddDays(-30);

        // 3 old records — should be pruned
        for (var i = 1; i <= 3; i++)
        {
            await service.RecordResultAsync(new CheckResult
            {
                EndpointId = endpointId,
                IsHealthy = true,
                Timestamp = cutoff.AddDays(-i)
            });
        }

        // 5 recent records — should survive
        for (var i = 1; i <= 5; i++)
        {
            await service.RecordResultAsync(new CheckResult
            {
                EndpointId = endpointId,
                IsHealthy = true,
                Timestamp = DateTime.UtcNow.AddMinutes(-i)
            });
        }

        var deleted = await PruneOlderThanAsync(cutoff);

        deleted.Should().Be(3);

        await using var verify = _fixture.CreateDbContext();
        var remaining = await verify.CheckResults
            .Where(r => r.EndpointId == endpointId)
            .CountAsync();

        remaining.Should().Be(5);
    }

    [Fact]
    public async Task Prune_WhenAllRecordsAreRecent_DeletesNothing()
    {
        var endpointId = await CreateEndpointAsync("Prune Nothing");

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

        var cutoff = now.AddDays(-90);
        var deleted = await PruneOlderThanAsync(cutoff);

        deleted.Should().Be(0);
    }

    [Fact]
    public async Task Prune_WhenAllRecordsAreOld_DeletesAll()
    {
        var endpointId = await CreateEndpointAsync("Prune All");

        await using var context = _fixture.CreateDbContext();
        var service = CreateCheckService(context);

        var cutoff = DateTime.UtcNow.AddDays(-30);

        for (var i = 1; i <= 8; i++)
        {
            await service.RecordResultAsync(new CheckResult
            {
                EndpointId = endpointId,
                IsHealthy = true,
                Timestamp = cutoff.AddDays(-i)
            });
        }

        var deleted = await PruneOlderThanAsync(cutoff);

        deleted.Should().Be(8);

        await using var verify = _fixture.CreateDbContext();
        var remaining = await verify.CheckResults
            .Where(r => r.EndpointId == endpointId)
            .CountAsync();

        remaining.Should().Be(0);
    }

    [Fact]
    public async Task Prune_WithRecordExactlyAtCutoff_DoesNotDeleteIt()
    {
        var endpointId = await CreateEndpointAsync("Boundary Cutoff");

        await using var context = _fixture.CreateDbContext();
        var service = CreateCheckService(context);

        var cutoff = DateTime.UtcNow.AddDays(-30);

        // Record exactly at the cutoff boundary — should NOT be deleted (query uses <, not <=)
        await service.RecordResultAsync(new CheckResult
        {
            EndpointId = endpointId,
            IsHealthy = true,
            Timestamp = cutoff
        });

        // One clearly old record — should be deleted
        await service.RecordResultAsync(new CheckResult
        {
            EndpointId = endpointId,
            IsHealthy = true,
            Timestamp = cutoff.AddDays(-1)
        });

        var deleted = await PruneOlderThanAsync(cutoff);

        deleted.Should().Be(1, "only the record before the cutoff should be deleted");

        await using var verify = _fixture.CreateDbContext();
        var remaining = await verify.CheckResults
            .Where(r => r.EndpointId == endpointId)
            .CountAsync();

        remaining.Should().Be(1);
    }

    [Fact]
    public async Task Prune_AcrossMultipleEndpoints_DeletesOldRecordsFromAll()
    {
        var endpointA = await CreateEndpointAsync("Prune Multi A");
        var endpointB = await CreateEndpointAsync("Prune Multi B");

        var cutoff = DateTime.UtcNow.AddDays(-30);

        await using var seedContext = _fixture.CreateDbContext();
        var seedService = CreateCheckService(seedContext);

        // 2 old + 3 recent for endpoint A
        await seedService.RecordResultAsync(new CheckResult { EndpointId = endpointA, IsHealthy = true, Timestamp = cutoff.AddDays(-1) });
        await seedService.RecordResultAsync(new CheckResult { EndpointId = endpointA, IsHealthy = true, Timestamp = cutoff.AddDays(-2) });
        for (var i = 1; i <= 3; i++)
            await seedService.RecordResultAsync(new CheckResult { EndpointId = endpointA, IsHealthy = true, Timestamp = DateTime.UtcNow.AddMinutes(-i) });

        // 4 old + 2 recent for endpoint B
        for (var i = 1; i <= 4; i++)
            await seedService.RecordResultAsync(new CheckResult { EndpointId = endpointB, IsHealthy = false, Timestamp = cutoff.AddDays(-i) });
        for (var i = 1; i <= 2; i++)
            await seedService.RecordResultAsync(new CheckResult { EndpointId = endpointB, IsHealthy = true, Timestamp = DateTime.UtcNow.AddMinutes(-i) });

        var deleted = await PruneOlderThanAsync(cutoff);

        deleted.Should().Be(6, "2 old from A + 4 old from B");

        await using var verify = _fixture.CreateDbContext();
        var remainingA = await verify.CheckResults.CountAsync(r => r.EndpointId == endpointA);
        var remainingB = await verify.CheckResults.CountAsync(r => r.EndpointId == endpointB);

        remainingA.Should().Be(3);
        remainingB.Should().Be(2);
    }
}
