using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace StatusTracker.Tests.Integration;

/// <summary>
/// Verifies that EF migrations apply cleanly against a real PostgreSQL instance
/// and that the expected schema objects exist after migration.
/// </summary>
[Collection("Database")]
[Trait("Category", "Integration")]
public sealed class MigrationTests(DatabaseFixture fixture)
{
    // ── Migration health ─────────────────────────────────────────────────────

    [Fact]
    public async Task Migrations_Apply_WithoutPendingMigrations()
    {
        await using var context = fixture.CreateDbContext();

        var pending = await context.Database.GetPendingMigrationsAsync();

        pending.Should().BeEmpty("all migrations should already be applied by the fixture");
    }

    [Fact]
    public async Task Migrations_Apply_CreateMonitoredEndpointsTable()
    {
        await using var context = fixture.CreateDbContext();

        // A simple query proves the table exists; an exception means the schema is missing
        var act = async () => await context.MonitoredEndpoints.AnyAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Migrations_Apply_CreateCheckResultsTable()
    {
        await using var context = fixture.CreateDbContext();

        var act = async () => await context.CheckResults.AnyAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Migrations_Apply_CreateSiteSettingsTable()
    {
        await using var context = fixture.CreateDbContext();

        var act = async () => await context.SiteSettings.AnyAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Migrations_Apply_CreateAspNetIdentityTables()
    {
        await using var context = fixture.CreateDbContext();

        // Identity tables are created as part of IdentityDbContext migrations
        var act = async () => await context.Users.AnyAsync();

        await act.Should().NotThrowAsync();
    }
}
