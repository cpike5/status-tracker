using Microsoft.Extensions.Options;
using StatusTracker.Infrastructure;

namespace StatusTracker.Services;

public class EmailWhitelistService : IEmailWhitelistService
{
    private readonly IReadOnlyList<string> _allowedEmails;

    public EmailWhitelistService(IOptions<AuthOptions> options)
    {
        _allowedEmails = options.Value.GetAllowedEmailList();
    }

    public bool IsAllowed(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        return _allowedEmails.Any(e => e.Equals(email, StringComparison.OrdinalIgnoreCase));
    }
}
