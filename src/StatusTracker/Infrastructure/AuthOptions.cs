namespace StatusTracker.Infrastructure;

public class AuthOptions
{
    public const string SectionName = "Auth";

    public string AllowedEmails { get; set; } = string.Empty;

    public IReadOnlyList<string> GetAllowedEmailList() =>
        AllowedEmails
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList()
            .AsReadOnly();
}
