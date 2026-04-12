namespace StatusTracker.Services;

public interface IEmailWhitelistService
{
    bool IsAllowed(string email);
}
