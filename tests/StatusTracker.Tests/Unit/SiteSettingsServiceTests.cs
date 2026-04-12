using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StatusTracker.Data;
using StatusTracker.Entities;
using StatusTracker.Services;

namespace StatusTracker.Tests.Unit;

/// <summary>
/// Unit tests for SiteSettingsService.
///
/// GetAsync tests use the EF InMemory provider.
/// UpdateAsync tests that exercise ExecuteUpdateAsync use SQLite in-memory, because
/// ExecuteUpdateAsync is not supported by the InMemory provider.
/// </summary>
[Trait("Category", "Unit")]
public class SiteSettingsServiceTests
{
    // ── Helpers ─────────────────────────────────────────────────────────────

    private static ApplicationDbContext CreateInMemoryContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    /// <summary>
    /// Creates a SQLite in-memory context. The caller is responsible for keeping the
    /// SqliteConnection open and disposing both it and the context when done.
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

    private static SiteSettingsService CreateService(ApplicationDbContext db) =>
        new(db, NullLogger<SiteSettingsService>.Instance);

    private static SiteSettings DefaultSettings() => new()
    {
        Id = 1,
        SiteTitle = "My Status Page",
        AccentColor = "#3d6ce7",
        FooterText = "Footer",
        LogoUrl = null,
    };

    // ── GetAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_SettingsExist_ReturnsSettings()
    {
        await using var db = CreateInMemoryContext(nameof(GetAsync_SettingsExist_ReturnsSettings));
        db.SiteSettings.Add(DefaultSettings());
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var result = await sut.GetAsync();

        result.Should().NotBeNull();
        result.SiteTitle.Should().Be("My Status Page");
    }

    [Fact]
    public async Task GetAsync_NoSettingsRow_ThrowsInvalidOperationException()
    {
        await using var db = CreateInMemoryContext(nameof(GetAsync_NoSettingsRow_ThrowsInvalidOperationException));
        var sut = CreateService(db);

        var act = () => sut.GetAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
              .WithMessage("*SiteSettings row is missing*");
    }

    // ── UpdateAsync — invalid color (throws before touching DB; InMemory is fine) ─────

    [Theory]
    [InlineData("3d6ce7")]        // missing # prefix
    [InlineData("#3d6ce")]        // 5 hex chars
    [InlineData("#3d6ce78")]      // 7 hex chars
    [InlineData("#gggggg")]       // invalid hex characters
    [InlineData("red")]           // named colour, not hex
    [InlineData("")]              // empty string
    [InlineData("rgba(0,0,0,1)")] // CSS function notation
    public async Task UpdateAsync_InvalidHexColor_ThrowsArgumentException(string hexColor)
    {
        await using var db = CreateInMemoryContext(
            $"SiteSettings_InvalidColor_{hexColor.Replace("#", "").Replace(",", "_").Replace("(", "").Replace(")", "")}");
        var sut = CreateService(db);

        var settings = new SiteSettings
        {
            SiteTitle = "Title",
            AccentColor = hexColor,
        };

        var act = () => sut.UpdateAsync(settings);

        await act.Should().ThrowAsync<ArgumentException>()
              .WithMessage("*valid hex color*");
    }

    // ── UpdateAsync — valid color (uses ExecuteUpdateAsync; requires SQLite) ──

    [Theory]
    [InlineData("#3d6ce7")]  // 6-char lowercase
    [InlineData("#3D6CE7")]  // 6-char uppercase
    [InlineData("#abc")]     // 3-char lowercase
    [InlineData("#ABC")]     // 3-char uppercase
    [InlineData("#a1B")]     // 3-char mixed
    [InlineData("#a1b2c3")]  // 6-char alphanumeric mixed
    public async Task UpdateAsync_ValidHexColor_DoesNotThrow(string hexColor)
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = CreateSqliteContext(connection);
        db.SiteSettings.Add(DefaultSettings());
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var settings = new SiteSettings
        {
            SiteTitle = "Updated Title",
            AccentColor = hexColor,
            FooterText = "New Footer",
            LogoUrl = null,
        };

        var act = () => sut.UpdateAsync(settings);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UpdateAsync_ValidSettings_UpdatesAllProperties()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = CreateSqliteContext(connection);
        db.SiteSettings.Add(DefaultSettings());
        await db.SaveChangesAsync();
        var sut = CreateService(db);

        var updated = new SiteSettings
        {
            SiteTitle = "New Title",
            AccentColor = "#ff5733",
            FooterText = "New Footer",
            LogoUrl = "https://example.com/logo.png",
        };

        await sut.UpdateAsync(updated);

        // ExecuteUpdateAsync bypasses the change tracker; read back with AsNoTracking
        // so we get the fresh values from the database.
        var stored = await db.SiteSettings.AsNoTracking().FirstAsync();
        stored.SiteTitle.Should().Be("New Title");
        stored.AccentColor.Should().Be("#ff5733");
        stored.FooterText.Should().Be("New Footer");
        stored.LogoUrl.Should().Be("https://example.com/logo.png");
    }

    [Fact]
    public async Task UpdateAsync_NoSettingsRow_ThrowsInvalidOperationException()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = CreateSqliteContext(connection);
        // Intentionally do not seed any SiteSettings row
        var sut = CreateService(db);

        var settings = new SiteSettings
        {
            SiteTitle = "Title",
            AccentColor = "#3d6ce7",
        };

        var act = () => sut.UpdateAsync(settings);

        await act.Should().ThrowAsync<InvalidOperationException>()
              .WithMessage("*SiteSettings row not found*");
    }
}
