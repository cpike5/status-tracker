using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Respawn;
using StatusTracker.Data;
using StatusTracker.Entities;
using Testcontainers.PostgreSql;

namespace StatusTracker.Tests.Integration;

/// <summary>
/// Starts a single PostgreSQL container for the entire test collection, applies migrations once,
/// and seeds the required SiteSettings row. Individual tests use Respawn to reset data between runs.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    private Respawner _respawner = null!;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        ConnectionString = _container.GetConnectionString();

        // Apply all EF migrations against the live container
        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();

        // Seed the mandatory SiteSettings row (mirrors Program.cs startup logic)
        if (!await context.SiteSettings.AnyAsync())
        {
            context.SiteSettings.Add(new SiteSettings
            {
                SiteTitle = "Status Tracker",
                AccentColor = "#3d6ce7",
                FooterText = "Powered by Status Tracker"
            });
            await context.SaveChangesAsync();
        }

        // Build a Respawner pointed at the public schema so we can wipe data between tests
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            // Leave Identity and migration history tables untouched
            TablesToIgnore =
            [
                new Respawn.Graph.Table("__EFMigrationsHistory"),
                new Respawn.Graph.Table("AspNetUsers"),
                new Respawn.Graph.Table("AspNetRoles"),
                new Respawn.Graph.Table("AspNetUserRoles"),
                new Respawn.Graph.Table("AspNetUserClaims"),
                new Respawn.Graph.Table("AspNetUserLogins"),
                new Respawn.Graph.Table("AspNetUserTokens"),
                new Respawn.Graph.Table("AspNetRoleClaims"),
                new Respawn.Graph.Table("SiteSettings"),
            ]
        });
    }

    /// <summary>
    /// Deletes all test data (except seeded/identity rows) so each test starts clean.
    /// Call this from each test class's lifecycle methods.
    /// </summary>
    public async Task ResetAsync()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await _respawner.ResetAsync(conn);
    }

    /// <summary>
    /// Restores the SiteSettings row to its seeded defaults.
    /// Because Respawn skips the SiteSettings table, tests that mutate the row
    /// must call this to ensure subsequent tests start from a known state.
    /// </summary>
    public async Task RestoreSiteSettingsAsync()
    {
        await using var context = CreateDbContext();

        var settings = await context.SiteSettings.FirstOrDefaultAsync();
        if (settings is null)
        {
            context.SiteSettings.Add(new SiteSettings
            {
                SiteTitle = "Status Tracker",
                AccentColor = "#3d6ce7",
                FooterText = "Powered by Status Tracker"
            });
        }
        else
        {
            settings.SiteTitle = "Status Tracker";
            settings.AccentColor = "#3d6ce7";
            settings.LogoUrl = null;
            settings.FooterText = "Powered by Status Tracker";
        }

        await context.SaveChangesAsync();
    }

    /// <summary>Creates a fresh DbContext backed by the test container database.</summary>
    public ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new ApplicationDbContext(options);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}

/// <summary>
/// xUnit collection definition — all integration test classes that share the same
/// PostgreSQL container must be decorated with [Collection("Database")].
/// </summary>
[CollectionDefinition("Database")]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>;
