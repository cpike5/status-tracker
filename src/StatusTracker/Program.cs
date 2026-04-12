using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using Serilog;
using StatusTracker.Components;
using StatusTracker.Data;
using StatusTracker.Entities;
using StatusTracker.Infrastructure;
using StatusTracker.Services;
using System.Security.Claims;

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

    // Database
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

    // HttpClientFactory for health checks
    builder.Services.AddHttpClient("HealthCheck");

    // FluentValidation
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();

    // Application services
    builder.Services.AddScoped<IEndpointService, EndpointService>();
    builder.Services.AddScoped<ICheckResultService, CheckResultService>();

    // Real-time update notifier — singleton so HealthCheckEngine (singleton) can signal Blazor circuits (scoped)
    builder.Services.AddSingleton<IStatusUpdateNotifier, StatusUpdateNotifier>();

    // Background services
    builder.Services.AddHostedService<HealthCheckEngine>();

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

    // Identity (OAuth-only — no local passwords)
    builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

    // Cookie configuration
    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/access-denied";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

    // Authorization with fallback policy — all routes require authentication by default
    builder.Services.AddAuthorization(options =>
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    });

    // Email whitelist enforcement service
    builder.Services.AddSingleton<IEmailWhitelistService, EmailWhitelistService>();

    // OAuth providers (conditionally enabled based on env vars)
    var authBuilder = builder.Services.AddAuthentication();

    // Shared whitelist enforcement for all OAuth providers
    static Task EnforceWhitelist(TicketReceivedContext context)
    {
        var whitelistService = context.HttpContext.RequestServices
            .GetRequiredService<IEmailWhitelistService>();
        var email = context.Principal?.FindFirstValue(ClaimTypes.Email);
        if (email is null || !whitelistService.IsAllowed(email))
        {
            context.Response.Redirect("/access-denied");
            context.HandleResponse();
        }
        return Task.CompletedTask;
    }

    var googleId = builder.Configuration["Google:ClientId"];
    var googleSecret = builder.Configuration["Google:ClientSecret"];
    if (!string.IsNullOrEmpty(googleId) && !string.IsNullOrEmpty(googleSecret))
    {
        authBuilder.AddGoogle(options =>
        {
            options.ClientId = googleId;
            options.ClientSecret = googleSecret;
            options.Scope.Add("email");
            options.Events.OnTicketReceived = EnforceWhitelist;
        });
    }

    var msId = builder.Configuration["Microsoft:ClientId"];
    var msSecret = builder.Configuration["Microsoft:ClientSecret"];
    if (!string.IsNullOrEmpty(msId) && !string.IsNullOrEmpty(msSecret))
    {
        authBuilder.AddMicrosoftAccount(options =>
        {
            options.ClientId = msId;
            options.ClientSecret = msSecret;
            options.Events.OnTicketReceived = EnforceWhitelist;
        });
    }

    var ghId = builder.Configuration["GitHub:ClientId"];
    var ghSecret = builder.Configuration["GitHub:ClientSecret"];
    if (!string.IsNullOrEmpty(ghId) && !string.IsNullOrEmpty(ghSecret))
    {
        authBuilder.AddGitHub(options =>
        {
            options.ClientId = ghId;
            options.ClientSecret = ghSecret;
            options.Scope.Add("user:email");
            options.Events.OnTicketReceived = EnforceWhitelist;
        });
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

        var schemes = app.Services.GetRequiredService<IAuthenticationSchemeProvider>();
        var configuredSchemes = schemes.GetAllSchemesAsync().GetAwaiter().GetResult();
        var oauthSchemes = configuredSchemes.Where(s =>
            s.Name == "Google" || s.Name == "Microsoft" || s.Name == "GitHub").ToList();
        if (oauthSchemes.Count == 0)
        {
            Log.Fatal("No OAuth providers configured. Set credentials for at least one provider (Google, Microsoft, or GitHub).");
            throw new InvalidOperationException("No OAuth providers configured.");
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

    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseAntiforgery();
    app.UseSerilogRequestLogging();

    app.MapStaticAssets();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    // Auth endpoints — minimal API for OAuth challenge and logout
    app.MapGet("/api/auth/login/{provider}", async (string provider, HttpContext context,
        IAuthenticationSchemeProvider schemeProvider) =>
    {
        var scheme = await schemeProvider.GetSchemeAsync(provider);
        if (scheme is null)
            return Results.BadRequest("Unknown provider.");

        var properties = new AuthenticationProperties { RedirectUri = "/" };
        await context.ChallengeAsync(provider, properties);
        return Results.Empty;
    }).AllowAnonymous();

    app.MapGet("/api/auth/logout", async (HttpContext context) =>
    {
        await context.SignOutAsync(IdentityConstants.ApplicationScheme);
        await context.SignOutAsync(IdentityConstants.ExternalScheme);
        return Results.Redirect("/login");
    }).AllowAnonymous();

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
