using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using StatusTracker.Entities;
using StatusTracker.Services;
using StatusTracker.Validators;

namespace StatusTracker.Tests.Integration;

/// <summary>
/// Integration tests for EndpointService CRUD operations and status computation
/// against a real PostgreSQL database.
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class EndpointServiceTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private readonly IValidator<MonitoredEndpoint> _validator = new MonitoredEndpointValidator();

    public EndpointServiceTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync() => await _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // ── Helpers ──────────────────────────────────────────────────────────────

    private EndpointService CreateService(StatusTracker.Data.ApplicationDbContext context) =>
        new(context, _validator, NullLogger<EndpointService>.Instance);

    private static MonitoredEndpoint BuildValidEndpoint(string name = "Test API", string url = "https://example.com") =>
        new()
        {
            Name = name,
            Url = url,
            CheckIntervalSeconds = 60,
            ExpectedStatusCode = 200,
            TimeoutSeconds = 10,
            RetryCount = 2,
            SortOrder = 0,
            IsEnabled = true
        };

    // ── CreateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_WithValidEndpoint_PersistsToDatabase()
    {
        await using var context = _fixture.CreateDbContext();
        var service = CreateService(context);

        var endpoint = BuildValidEndpoint("My Service", "https://my-service.example.com");

        var created = await service.CreateAsync(endpoint);

        created.Id.Should().BeGreaterThan(0);

        // Verify it is persisted by re-fetching with a fresh context
        await using var verify = _fixture.CreateDbContext();
        var persisted = await verify.MonitoredEndpoints.FindAsync(created.Id);
        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be("My Service");
        persisted.Url.Should().Be("https://my-service.example.com");
    }

    [Fact]
    public async Task CreateAsync_WithValidEndpoint_SetsCreatedAtAndUpdatedAt()
    {
        await using var context = _fixture.CreateDbContext();
        var service = CreateService(context);

        var before = DateTime.UtcNow.AddSeconds(-1);
        var created = await service.CreateAsync(BuildValidEndpoint());
        var after = DateTime.UtcNow.AddSeconds(1);

        created.CreatedAt.Should().BeAfter(before).And.BeBefore(after);
        created.UpdatedAt.Should().BeAfter(before).And.BeBefore(after);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidUrl_ThrowsValidationException()
    {
        await using var context = _fixture.CreateDbContext();
        var service = CreateService(context);

        var invalid = BuildValidEndpoint(url: "not-a-url");

        var act = async () => await service.CreateAsync(invalid);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task CreateAsync_WithEmptyName_ThrowsValidationException()
    {
        await using var context = _fixture.CreateDbContext();
        var service = CreateService(context);

        var invalid = BuildValidEndpoint(name: string.Empty);

        var act = async () => await service.CreateAsync(invalid);

        await act.Should().ThrowAsync<ValidationException>();
    }

    // ── GetAllAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_WithNoEndpoints_ReturnsEmptyList()
    {
        await using var context = _fixture.CreateDbContext();
        var service = CreateService(context);

        var result = await service.GetAllAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_WithMultipleEndpoints_ReturnsAll()
    {
        await using var context = _fixture.CreateDbContext();
        var service = CreateService(context);

        await service.CreateAsync(BuildValidEndpoint("Service A", "https://a.example.com"));
        await service.CreateAsync(BuildValidEndpoint("Service B", "https://b.example.com"));
        await service.CreateAsync(BuildValidEndpoint("Service C", "https://c.example.com"));

        var result = await service.GetAllAsync();

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAllAsync_WithMixedEnabledState_ReturnsAllEndpoints()
    {
        await using var context = _fixture.CreateDbContext();
        var service = CreateService(context);

        var enabled = BuildValidEndpoint("Enabled", "https://enabled.example.com");
        enabled.IsEnabled = true;

        var disabled = BuildValidEndpoint("Disabled", "https://disabled.example.com");
        disabled.IsEnabled = false;

        await service.CreateAsync(enabled);
        await service.CreateAsync(disabled);

        var result = await service.GetAllAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_OrdersBySortOrderThenName()
    {
        await using var context = _fixture.CreateDbContext();
        var service = CreateService(context);

        var b = BuildValidEndpoint("B Service", "https://b.example.com");
        b.SortOrder = 1;

        var a = BuildValidEndpoint("A Service", "https://a.example.com");
        a.SortOrder = 1;

        var first = BuildValidEndpoint("First", "https://first.example.com");
        first.SortOrder = 0;

        await service.CreateAsync(b);
        await service.CreateAsync(a);
        await service.CreateAsync(first);

        var result = await service.GetAllAsync();

        result[0].Name.Should().Be("First");
        result[1].Name.Should().Be("A Service");
        result[2].Name.Should().Be("B Service");
    }

    // ── GetEnabledAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetEnabledAsync_ReturnsOnlyEnabledEndpoints()
    {
        await using var context = _fixture.CreateDbContext();
        var service = CreateService(context);

        var enabled = BuildValidEndpoint("Enabled", "https://enabled.example.com");
        enabled.IsEnabled = true;

        var disabled = BuildValidEndpoint("Disabled", "https://disabled.example.com");
        disabled.IsEnabled = false;

        await service.CreateAsync(enabled);
        await service.CreateAsync(disabled);

        var result = await service.GetEnabledAsync();

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Enabled");
    }

    // ── GetByIdAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsEndpoint()
    {
        await using var context = _fixture.CreateDbContext();
        var service = CreateService(context);

        var created = await service.CreateAsync(BuildValidEndpoint("Lookup Target", "https://target.example.com"));

        var result = await service.GetByIdAsync(created.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Lookup Target");
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ReturnsNull()
    {
        await using var context = _fixture.CreateDbContext();
        var service = CreateService(context);

        var result = await service.GetByIdAsync(999_999);

        result.Should().BeNull();
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_WithValidChanges_PersistsToDatabase()
    {
        await using var context = _fixture.CreateDbContext();
        var service = CreateService(context);

        var endpoint = await service.CreateAsync(BuildValidEndpoint("Original Name", "https://original.example.com"));

        endpoint.Name = "Updated Name";
        endpoint.Url = "https://updated.example.com";

        await service.UpdateAsync(endpoint);

        await using var verify = _fixture.CreateDbContext();
        var persisted = await verify.MonitoredEndpoints.FindAsync(endpoint.Id);
        persisted!.Name.Should().Be("Updated Name");
        persisted.Url.Should().Be("https://updated.example.com");
    }

    [Fact]
    public async Task UpdateAsync_WithInvalidUrl_ThrowsValidationException()
    {
        await using var context = _fixture.CreateDbContext();
        var service = CreateService(context);

        var endpoint = await service.CreateAsync(BuildValidEndpoint());
        endpoint.Url = "not-a-valid-url";

        var act = async () => await service.UpdateAsync(endpoint);

        await act.Should().ThrowAsync<ValidationException>();
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_WithExistingId_RemovesFromDatabase()
    {
        await using var context = _fixture.CreateDbContext();
        var service = CreateService(context);

        var endpoint = await service.CreateAsync(BuildValidEndpoint("To Delete", "https://delete.example.com"));

        await service.DeleteAsync(endpoint.Id);

        await using var verify = _fixture.CreateDbContext();
        var persisted = await verify.MonitoredEndpoints.FindAsync(endpoint.Id);
        persisted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentId_DoesNotThrow()
    {
        await using var context = _fixture.CreateDbContext();
        var service = CreateService(context);

        var act = async () => await service.DeleteAsync(999_999);

        await act.Should().NotThrowAsync();
    }

    // ── ToggleEnabledAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task ToggleEnabledAsync_WhenEnabled_DisablesEndpoint()
    {
        await using var context = _fixture.CreateDbContext();
        var service = CreateService(context);

        var endpoint = BuildValidEndpoint("Toggle Me", "https://toggle.example.com");
        endpoint.IsEnabled = true;
        var created = await service.CreateAsync(endpoint);

        await service.ToggleEnabledAsync(created.Id);

        await using var verify = _fixture.CreateDbContext();
        var persisted = await verify.MonitoredEndpoints.FindAsync(created.Id);
        persisted!.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleEnabledAsync_WhenDisabled_EnablesEndpoint()
    {
        await using var context = _fixture.CreateDbContext();
        var service = CreateService(context);

        var endpoint = BuildValidEndpoint("Toggle Me Back", "https://toggle-back.example.com");
        endpoint.IsEnabled = false;
        var created = await service.CreateAsync(endpoint);

        await service.ToggleEnabledAsync(created.Id);

        await using var verify = _fixture.CreateDbContext();
        var persisted = await verify.MonitoredEndpoints.FindAsync(created.Id);
        persisted!.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleEnabledAsync_WithNonExistentId_DoesNotThrow()
    {
        await using var context = _fixture.CreateDbContext();
        var service = CreateService(context);

        var act = async () => await service.ToggleEnabledAsync(999_999);

        await act.Should().NotThrowAsync();
    }

    // ── GetWithStatusAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetWithStatusAsync_WithNoEndpoints_ReturnsEmptyList()
    {
        await using var context = _fixture.CreateDbContext();
        var service = CreateService(context);

        var result = await service.GetWithStatusAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetWithStatusAsync_WithEndpointButNoChecks_ReturnsUnknownStatus()
    {
        await using var context = _fixture.CreateDbContext();
        var service = CreateService(context);

        await service.CreateAsync(BuildValidEndpoint("No Checks Yet", "https://nochecks.example.com"));

        var result = await service.GetWithStatusAsync();

        result.Should().HaveCount(1);
        result[0].Status.Should().Be(EndpointStatus.Unknown);
        result[0].Uptime24h.Should().BeNull();
    }
}
