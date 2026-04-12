namespace StatusTracker.Entities;

public class SiteSettings
{
    public int Id { get; set; }
    public string SiteTitle { get; set; } = "Status Tracker";
    public string? LogoUrl { get; set; }
    public string AccentColor { get; set; } = "#3d6ce7";
    public string? FooterText { get; set; }
}
