using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StatusTracker.Entities;
using StatusTracker.Services;

namespace StatusTracker.Tests.Integration;

/// <summary>
/// Verifies that SiteSettings are seeded on first startup and that
/// SiteSettingsService can read and update the settings row.
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class SiteSettingsTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;

    public SiteSettingsTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Reset mutable data tables first, then restore the canonical SiteSettings row
        // so every test in this class starts from the seeded defaults.
        await _fixture.ResetAsync();
        await _fixture.RestoreSiteSettingsAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── Seeding ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Seeding_OnFirstStartup_CreatesSiteSettingsRow()
    {
        await using var context = _fixture.CreateDbContext();

        var count = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .CountAsync(context.SiteSettings);

        count.Should().Be(1, "the fixture seeds exactly one SiteSettings row");
    }

    [Fact]
    public async Task Seeding_OnFirstStartup_SetsDefaultTitle()
    {
        await using var context = _fixture.CreateDbContext();
        var service = new SiteSettingsService(context, NullLogger<SiteSettingsService>.Instance);

        var settings = await service.GetAsync();

        settings.SiteTitle.Should().Be("Status Tracker");
    }

    [Fact]
    public async Task Seeding_OnFirstStartup_SetsDefaultAccentColor()
    {
        await using var context = _fixture.CreateDbContext();
        var service = new SiteSettingsService(context, NullLogger<SiteSettingsService>.Instance);

        var settings = await service.GetAsync();

        settings.AccentColor.Should().Be("#3d6ce7");
    }

    // ── GetAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_WhenRowExists_ReturnsSiteSettings()
    {
        await using var context = _fixture.CreateDbContext();
        var service = new SiteSettingsService(context, NullLogger<SiteSettingsService>.Instance);

        var settings = await service.GetAsync();

        settings.Should().NotBeNull();
        settings.Id.Should().BeGreaterThan(0);
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_WithValidSettings_PersistsChanges()
    {
        await using var context = _fixture.CreateDbContext();
        var service = new SiteSettingsService(context, NullLogger<SiteSettingsService>.Instance);

        var updated = new SiteSettings
        {
            SiteTitle = "My Company Status",
            AccentColor = "#ff5733",
            LogoUrl = "https://example.com/logo.png",
            FooterText = "© 2026 My Company"
        };

        await service.UpdateAsync(updated);

        // Read back using a separate context to confirm persistence
        await using var verifyContext = _fixture.CreateDbContext();
        var verifyService = new SiteSettingsService(verifyContext, NullLogger<SiteSettingsService>.Instance);
        var persisted = await verifyService.GetAsync();

        persisted.SiteTitle.Should().Be("My Company Status");
        persisted.AccentColor.Should().Be("#ff5733");
        persisted.LogoUrl.Should().Be("https://example.com/logo.png");
        persisted.FooterText.Should().Be("© 2026 My Company");
    }

    [Fact]
    public async Task UpdateAsync_WithInvalidHexColor_ThrowsArgumentException()
    {
        await using var context = _fixture.CreateDbContext();
        var service = new SiteSettingsService(context, NullLogger<SiteSettingsService>.Instance);

        var invalid = new SiteSettings
        {
            SiteTitle = "Test",
            AccentColor = "not-a-hex-color"
        };

        var act = async () => await service.UpdateAsync(invalid);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*AccentColor*");
    }

    [Fact]
    public async Task UpdateAsync_WithShortHexColor_PersistsChanges()
    {
        await using var context = _fixture.CreateDbContext();
        var service = new SiteSettingsService(context, NullLogger<SiteSettingsService>.Instance);

        // Three-character hex codes are valid (#RGB)
        var updated = new SiteSettings
        {
            SiteTitle = "Short Hex Test",
            AccentColor = "#abc"
        };

        var act = async () => await service.UpdateAsync(updated);

        await act.Should().NotThrowAsync();
    }
}
