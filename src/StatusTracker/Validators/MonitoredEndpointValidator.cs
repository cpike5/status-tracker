using System.Text.RegularExpressions;
using FluentValidation;
using StatusTracker.Entities;

namespace StatusTracker.Validators;

public class MonitoredEndpointValidator : AbstractValidator<MonitoredEndpoint>
{
    public MonitoredEndpointValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(200);

        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("URL is required")
            .MaximumLength(2048)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri)
                         && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .WithMessage("URL must be a valid HTTP or HTTPS URL");

        RuleFor(x => x.CheckIntervalSeconds)
            .InclusiveBetween(10, 3600)
            .WithMessage("Check interval must be between 10 seconds and 1 hour");

        RuleFor(x => x.ExpectedStatusCode)
            .InclusiveBetween(100, 599)
            .WithMessage("Expected status code must be between 100 and 599");

        RuleFor(x => x.TimeoutSeconds)
            .InclusiveBetween(1, 60)
            .WithMessage("Timeout must be between 1 and 60 seconds");

        RuleFor(x => x.TimeoutSeconds)
            .LessThan(x => x.CheckIntervalSeconds)
            .WithMessage("Timeout must be less than the check interval");

        RuleFor(x => x.RetryCount)
            .InclusiveBetween(0, 5)
            .WithMessage("Retry count must be between 0 and 5");

        RuleFor(x => x.SortOrder)
            .InclusiveBetween(0, 9999);

        RuleFor(x => x.Group)
            .MaximumLength(100);

        RuleFor(x => x.ExpectedBodyMatch)
            .MaximumLength(1000)
            .Must(BeValidRegexOrNull)
            .WithMessage("Expected body match must be a valid regular expression");
    }

    private static bool BeValidRegexOrNull(string? pattern)
    {
        if (string.IsNullOrEmpty(pattern))
            return true;

        try
        {
            Regex.Match(string.Empty, pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
            return true;
        }
        catch (RegexParseException)
        {
            return false;
        }
    }
}
