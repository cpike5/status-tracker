using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using StatusTracker.Data;
using StatusTracker.Entities;

namespace StatusTracker.Services;

public partial class SiteSettingsService : ISiteSettingsService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<SiteSettingsService> _logger;

    public SiteSettingsService(ApplicationDbContext db, ILogger<SiteSettingsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<SiteSettings> GetAsync()
    {
        return await _db.SiteSettings.FirstOrDefaultAsync()
            ?? throw new InvalidOperationException(
                "SiteSettings row is missing. Run database migrations and ensure seeding completed.");
    }

    public async Task UpdateAsync(SiteSettings settings)
    {
        if (!HexColorRegex().IsMatch(settings.AccentColor))
            throw new ArgumentException("AccentColor must be a valid hex color (#RGB or #RRGGBB).");

        var rows = await _db.SiteSettings.ExecuteUpdateAsync(s => s
            .SetProperty(x => x.SiteTitle, settings.SiteTitle)
            .SetProperty(x => x.LogoUrl, settings.LogoUrl)
            .SetProperty(x => x.AccentColor, settings.AccentColor)
            .SetProperty(x => x.FooterText, settings.FooterText));

        if (rows == 0)
            throw new InvalidOperationException("SiteSettings row not found.");

        _logger.LogInformation("Site settings updated (Title: {SiteTitle}, AccentColor: {AccentColor})",
            settings.SiteTitle, settings.AccentColor);
    }

    [GeneratedRegex(@"^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")]
    private static partial Regex HexColorRegex();
}
