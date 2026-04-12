using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StatusTracker.Data;
using StatusTracker.Entities;
using StatusTracker.Services;

namespace StatusTracker.Tests.Unit;

/// <summary>
/// Unit tests for EndpointService.
///
/// Most tests use the EF InMemory provider for speed. Tests that exercise
/// ExecuteDeleteAsync (DeleteAsync) require SQLite, because the InMemory provider
/// does not support bulk-operation methods.
/// </summary>
[Trait("Category", "Unit")]
public class EndpointServiceTests
{
    // ── Helpers ─────────────────────────────────────────────────────────────

    private static ApplicationDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    /// <summary>
    /// Creates a SQLite in-memory context backed by the supplied open connection.
    /// The caller owns the connection lifetime and must dispose both connection and context.
    /// The schema is created automatically on first call.
    /// </summary>
    private static ApplicationDbContext CreateSqliteContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        var ctx = new ApplicationDbContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    /// <summary>Creates a validator that always reports valid.</summary>
    private static IValidator<MonitoredEndpoint> AlwaysValidValidator()
    {
        var v = Substitute.For<IValidator<MonitoredEndpoint>>();
        v.ValidateAsync(Arg.Any<MonitoredEndpoint>(), Arg.Any<CancellationToken>())
         .Returns(new ValidationResult());
        return v;
    }

    /// <summary>Creates a validator that always reports invalid with one error.</summary>
    private static IValidator<MonitoredEndpoint> AlwaysInvalidValidator()
    {
        var v = Substitute.For<IValidator<MonitoredEndpoint>>();
        var failures = new List<ValidationFailure>
        {
            new(nameof(MonitoredEndpoint.Name), "Name is required")
        };
        v.ValidateAsync(Arg.Any<MonitoredEndpoint>(), Arg.Any<CancellationToken>())
         .Returns(new ValidationResult(failures));
        return v;
    }

    private static EndpointService CreateService(
        ApplicationDbContext db,
        IValidator<MonitoredEndpoint>? validator = null)
    {
        return new EndpointService(
            db,
            validator ?? AlwaysValidValidator(),
            NullLogger<EndpointService>.Instance);
    }

    private static MonitoredEndpoint MakeEndpoint(string name = "Test", int sortOrder = 0) => new()
    {
        Name = name,
        Url = "https://example.com",
        CheckIntervalSeconds = 60,
        ExpectedStatusCode = 200,
        TimeoutSeconds = 10,
        RetryCount = 2,
        SortOrder = sortOrder,
        IsEnabled = true,
    };

    // ── GetAllAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsAllEndpoints()
    {
        await using var db = CreateContext(nameof(GetAllAsync_ReturnsAllEndpoints));
        db.MonitoredEndpoints.AddRange(MakeEndpoint("A"), MakeEndpoint("B"));
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var result = await sut.GetAllAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_OrdersBySortOrderThenName()
    {
        await using var db = CreateContext(nameof(GetAllAsync_OrdersBySortOrderThenName));
        db.MonitoredEndpoints.AddRange(
            MakeEndpoint("Zebra", sortOrder: 1),
            MakeEndpoint("Alpha", sortOrder: 1),
            MakeEndpoint("Middle", sortOrder: 0));
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var result = await sut.GetAllAsync();

        result.Select(e => e.Name).Should().Equal("Middle", "Alpha", "Zebra");
    }

    [Fact]
    public async Task GetAllAsync_EmptyDatabase_ReturnsEmptyList()
    {
        await using var db = CreateContext(nameof(GetAllAsync_EmptyDatabase_ReturnsEmptyList));
        var sut = CreateService(db);

        var result = await sut.GetAllAsync();

        result.Should().BeEmpty();
    }

    // ── GetByIdAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsEndpoint()
    {
        await using var db = CreateContext(nameof(GetByIdAsync_ExistingId_ReturnsEndpoint));
        var endpoint = MakeEndpoint("Found");
        db.MonitoredEndpoints.Add(endpoint);
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var result = await sut.GetByIdAsync(endpoint.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Found");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        await using var db = CreateContext(nameof(GetByIdAsync_NonExistentId_ReturnsNull));
        var sut = CreateService(db);

        var result = await sut.GetByIdAsync(99999);

        result.Should().BeNull();
    }

    // ── CreateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ValidEndpoint_PersistsToDatabase()
    {
        await using var db = CreateContext(nameof(CreateAsync_ValidEndpoint_PersistsToDatabase));
        var sut = CreateService(db);
        var endpoint = MakeEndpoint("New Service");

        var created = await sut.CreateAsync(endpoint);

        db.MonitoredEndpoints.Should().ContainSingle(e => e.Name == "New Service");
        created.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateAsync_ValidEndpoint_SetsCreatedAtAndUpdatedAt()
    {
        await using var db = CreateContext(nameof(CreateAsync_ValidEndpoint_SetsCreatedAtAndUpdatedAt));
        var sut = CreateService(db);
        var endpoint = MakeEndpoint();
        // Deliberately set timestamps to default to confirm the service overwrites them
        endpoint.CreatedAt = default;
        endpoint.UpdatedAt = default;

        var before = DateTime.UtcNow;
        await sut.CreateAsync(endpoint);
        var after = DateTime.UtcNow;

        endpoint.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        endpoint.UpdatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task CreateAsync_InvalidEndpoint_ThrowsValidationException()
    {
        await using var db = CreateContext(nameof(CreateAsync_InvalidEndpoint_ThrowsValidationException));
        var sut = CreateService(db, AlwaysInvalidValidator());
        var endpoint = MakeEndpoint();

        var act = () => sut.CreateAsync(endpoint);

        await act.Should().ThrowAsync<ValidationException>();
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ValidEndpoint_PersistsChanges()
    {
        await using var db = CreateContext(nameof(UpdateAsync_ValidEndpoint_PersistsChanges));
        var endpoint = MakeEndpoint("Original");
        db.MonitoredEndpoints.Add(endpoint);
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        endpoint.Name = "Updated";
        await sut.UpdateAsync(endpoint);

        var stored = await db.MonitoredEndpoints.FindAsync(endpoint.Id);
        stored!.Name.Should().Be("Updated");
    }

    [Fact]
    public async Task UpdateAsync_ValidEndpoint_SetsUpdatedAt()
    {
        await using var db = CreateContext(nameof(UpdateAsync_ValidEndpoint_SetsUpdatedAt));
        var endpoint = MakeEndpoint();
        endpoint.UpdatedAt = DateTime.UtcNow.AddDays(-1); // stale timestamp
        db.MonitoredEndpoints.Add(endpoint);
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var before = DateTime.UtcNow;
        await sut.UpdateAsync(endpoint);
        var after = DateTime.UtcNow;

        endpoint.UpdatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task UpdateAsync_InvalidEndpoint_ThrowsValidationException()
    {
        await using var db = CreateContext(nameof(UpdateAsync_InvalidEndpoint_ThrowsValidationException));
        var endpoint = MakeEndpoint();
        db.MonitoredEndpoints.Add(endpoint);
        await db.SaveChangesAsync();
        var sut = CreateService(db, AlwaysInvalidValidator());

        var act = () => sut.UpdateAsync(endpoint);

        await act.Should().ThrowAsync<ValidationException>();
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────────
    // ExecuteDeleteAsync is not supported by the InMemory provider, so these
    // tests use a SQLite in-memory database instead.

    [Fact]
    public async Task DeleteAsync_ExistingId_RemovesEndpoint()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = CreateSqliteContext(connection);

        var endpoint = MakeEndpoint("ToDelete");
        db.MonitoredEndpoints.Add(endpoint);
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        await sut.DeleteAsync(endpoint.Id);

        db.MonitoredEndpoints.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_NonExistentId_DoesNotThrow()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = CreateSqliteContext(connection);
        var sut = CreateService(db);

        var act = () => sut.DeleteAsync(99999);

        await act.Should().NotThrowAsync();
    }

    // ── ToggleEnabledAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task ToggleEnabledAsync_EnabledEndpoint_DisablesIt()
    {
        await using var db = CreateContext(nameof(ToggleEnabledAsync_EnabledEndpoint_DisablesIt));
        var endpoint = MakeEndpoint();
        endpoint.IsEnabled = true;
        db.MonitoredEndpoints.Add(endpoint);
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        await sut.ToggleEnabledAsync(endpoint.Id);

        var stored = await db.MonitoredEndpoints.FindAsync(endpoint.Id);
        stored!.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleEnabledAsync_DisabledEndpoint_EnablesIt()
    {
        await using var db = CreateContext(nameof(ToggleEnabledAsync_DisabledEndpoint_EnablesIt));
        var endpoint = MakeEndpoint();
        endpoint.IsEnabled = false;
        db.MonitoredEndpoints.Add(endpoint);
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        await sut.ToggleEnabledAsync(endpoint.Id);

        var stored = await db.MonitoredEndpoints.FindAsync(endpoint.Id);
        stored!.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleEnabledAsync_NonExistentId_DoesNotThrow()
    {
        await using var db = CreateContext(nameof(ToggleEnabledAsync_NonExistentId_DoesNotThrow));
        var sut = CreateService(db);

        var act = () => sut.ToggleEnabledAsync(99999);

        await act.Should().NotThrowAsync();
    }

    // ── DetermineStatus (via GetWithStatusAsync) ─────────────────────────────
    // The private DetermineStatus method is exercised through GetWithStatusAsync
    // by controlling the check results seeded into the database.

    [Fact]
    public async Task GetWithStatusAsync_NoCheckResults_ReturnsUnknownStatus()
    {
        await using var db = CreateContext(nameof(GetWithStatusAsync_NoCheckResults_ReturnsUnknownStatus));
        db.MonitoredEndpoints.Add(MakeEndpoint());
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var result = await sut.GetWithStatusAsync();

        result.Should().ContainSingle()
              .Which.Status.Should().Be(EndpointStatus.Unknown);
    }

    [Fact]
    public async Task GetWithStatusAsync_LatestResultIsHealthy_NoAnomalies_ReturnsUp()
    {
        await using var db = CreateContext(nameof(GetWithStatusAsync_LatestResultIsHealthy_NoAnomalies_ReturnsUp));
        var endpoint = MakeEndpoint();
        db.MonitoredEndpoints.Add(endpoint);
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        db.CheckResults.Add(new CheckResult
        {
            EndpointId = endpoint.Id,
            Timestamp = now.AddMinutes(-1),
            IsHealthy = true,
            ResponseTimeMs = 100,
        });
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var result = await sut.GetWithStatusAsync();

        result.Should().ContainSingle().Which.Status.Should().Be(EndpointStatus.Up);
    }

    [Fact]
    public async Task GetWithStatusAsync_LatestResultIsUnhealthy_ReturnsDown()
    {
        await using var db = CreateContext(nameof(GetWithStatusAsync_LatestResultIsUnhealthy_ReturnsDown));
        var endpoint = MakeEndpoint();
        db.MonitoredEndpoints.Add(endpoint);
        await db.SaveChangesAsync();

        db.CheckResults.Add(new CheckResult
        {
            EndpointId = endpoint.Id,
            Timestamp = DateTime.UtcNow.AddMinutes(-1),
            IsHealthy = false,
        });
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var result = await sut.GetWithStatusAsync();

        result.Should().ContainSingle().Which.Status.Should().Be(EndpointStatus.Down);
    }

    [Fact]
    public async Task GetWithStatusAsync_HighUnhealthyRatioLastHour_ReturnsDegraded()
    {
        // Seed: 4 unhealthy + 1 healthy in last hour → 80% unhealthy, which is > 20% threshold.
        // The latest result is healthy so it doesn't flip to Down.
        await using var db = CreateContext(nameof(GetWithStatusAsync_HighUnhealthyRatioLastHour_ReturnsDegraded));
        var endpoint = MakeEndpoint();
        db.MonitoredEndpoints.Add(endpoint);
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        // Latest check is healthy
        db.CheckResults.Add(new CheckResult
        {
            EndpointId = endpoint.Id,
            Timestamp = now.AddMinutes(-1),
            IsHealthy = true,
            ResponseTimeMs = 100,
        });
        // 4 unhealthy checks within last hour
        for (var i = 2; i <= 5; i++)
        {
            db.CheckResults.Add(new CheckResult
            {
                EndpointId = endpoint.Id,
                Timestamp = now.AddMinutes(-i),
                IsHealthy = false,
            });
        }
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var result = await sut.GetWithStatusAsync();

        result.Should().ContainSingle().Which.Status.Should().Be(EndpointStatus.Degraded);
    }

    [Fact]
    public async Task GetWithStatusAsync_ResponseTimeExceedsFiveTimesAverage_ReturnsDegraded()
    {
        // We need: spike > 5 × avg_including_spike.
        // With N=10 baseline checks at 100 ms and spike=900 ms:
        //   avg = (10×100 + 900) / 11 ≈ 172 ms
        //   5× avg ≈ 863 ms → 900 ms > 863 ms  → Degraded
        await using var db = CreateContext(nameof(GetWithStatusAsync_ResponseTimeExceedsFiveTimesAverage_ReturnsDegraded));
        var endpoint = MakeEndpoint();
        db.MonitoredEndpoints.Add(endpoint);
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        // 10 healthy checks with low response times within the last hour
        for (var i = 2; i <= 11; i++)
        {
            db.CheckResults.Add(new CheckResult
            {
                EndpointId = endpoint.Id,
                Timestamp = now.AddMinutes(-i),
                IsHealthy = true,
                ResponseTimeMs = 100,
            });
        }
        // Latest healthy check with a spike response time (must be the most recent)
        db.CheckResults.Add(new CheckResult
        {
            EndpointId = endpoint.Id,
            Timestamp = now.AddMinutes(-1),
            IsHealthy = true,
            ResponseTimeMs = 900,
        });
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var result = await sut.GetWithStatusAsync();

        result.Should().ContainSingle().Which.Status.Should().Be(EndpointStatus.Degraded);
    }

    [Fact]
    public async Task GetWithStatusAsync_EmptyEndpointList_ReturnsEmptyList()
    {
        await using var db = CreateContext(nameof(GetWithStatusAsync_EmptyEndpointList_ReturnsEmptyList));
        var sut = CreateService(db);

        var result = await sut.GetWithStatusAsync();

        result.Should().BeEmpty();
    }
}
