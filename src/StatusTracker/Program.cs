using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using Serilog;
using StatusTracker.Components;
using StatusTracker.Data;
using StatusTracker.Entities;
using StatusTracker.Infrastructure;

// Stage 1: Bootstrap logger (captures logs before DI is built)
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Status Tracker");

    var builder = WebApplication.CreateBuilder(args);

    // Stage 2: Full Serilog configuration from appsettings
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithThreadId());

    // Add services to the container.
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddMudServices();

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

    // Configuration pipeline
    builder.Services.Configure<HealthCheckOptions>(
        builder.Configuration.GetSection(HealthCheckOptions.SectionName));
    builder.Services.Configure<DataRetentionOptions>(
        builder.Configuration.GetSection(DataRetentionOptions.SectionName));
    builder.Services.Configure<AuthOptions>(
        builder.Configuration.GetSection(AuthOptions.SectionName));

    // Map ALLOWED_EMAILS env var to Auth:AllowedEmails config key
    if (builder.Configuration["ALLOWED_EMAILS"] is { } allowedEmails)
    {
        builder.Configuration["Auth:AllowedEmails"] = allowedEmails;
    }

    var app = builder.Build();

    // Fail-fast: validate required configuration (skip in Development for local testing)
    if (!app.Environment.IsDevelopment())
    {
        var connectionString = app.Configuration.GetConnectionString("Postgres");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Log.Fatal("Required configuration 'ConnectionStrings:Postgres' is missing. Set the ConnectionStrings__Postgres environment variable.");
            throw new InvalidOperationException("Required configuration 'ConnectionStrings:Postgres' is missing.");
        }

        var authOptions = app.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>();
        if (authOptions is null || string.IsNullOrWhiteSpace(authOptions.AllowedEmails))
        {
            Log.Fatal("Required configuration 'Auth:AllowedEmails' is missing. Set the ALLOWED_EMAILS environment variable.");
            throw new InvalidOperationException("Required configuration 'Auth:AllowedEmails' is missing.");
        }
    }

    // Auto-migrate database on startup (fail-fast if migration fails)
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        try
        {
            await db.Database.MigrateAsync();
            Log.Information("Database migration completed successfully");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Database migration failed");
            throw;
        }

        // Seed default SiteSettings if table is empty
        if (!await db.SiteSettings.AnyAsync())
        {
            db.SiteSettings.Add(new SiteSettings
            {
                SiteTitle = "Status Tracker",
                AccentColor = "#3d6ce7",
                FooterText = "Powered by Status Tracker"
            });
            await db.SaveChangesAsync();
            Log.Information("Default SiteSettings seeded");
        }
    }

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
    }

    app.UseAntiforgery();
    app.UseSerilogRequestLogging();

    app.MapStaticAssets();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
