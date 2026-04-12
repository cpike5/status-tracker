using StatusTracker.Entities;

namespace StatusTracker.Services;

public interface ISiteSettingsService
{
    Task<SiteSettings> GetAsync();
    Task UpdateAsync(SiteSettings settings);
}
