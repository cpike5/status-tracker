using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using StatusTracker.Entities;
using StatusTracker.Validators;

namespace StatusTracker.Tests.Unit;

/// <summary>
/// Tests for MonitoredEndpointValidator — one test per validation rule covering
/// valid input, boundary values, and invalid input.
/// </summary>
[Trait("Category", "Unit")]
public class MonitoredEndpointValidatorTests
{
    private readonly MonitoredEndpointValidator _sut = new();

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>Returns a fully-valid endpoint that satisfies every rule.</summary>
    private static MonitoredEndpoint ValidEndpoint() => new()
    {
        Name = "My Service",
        Url = "https://example.com/health",
        CheckIntervalSeconds = 60,
        ExpectedStatusCode = 200,
        TimeoutSeconds = 10,
        RetryCount = 2,
        SortOrder = 0,
        Group = null,
        ExpectedBodyMatch = null,
    };

    private ValidationResult Validate(MonitoredEndpoint e) => _sut.Validate(e);

    // ── Name ────────────────────────────────────────────────────────────────

    [Fact]
    public void Name_Valid_PassesValidation()
    {
        var endpoint = ValidEndpoint();

        var result = Validate(endpoint);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Name_Empty_FailsWithRequiredMessage()
    {
        var endpoint = ValidEndpoint();
        endpoint.Name = string.Empty;

        var result = Validate(endpoint);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(MonitoredEndpoint.Name)
                                            && e.ErrorMessage.Contains("required"));
    }

    [Fact]
    public void Name_Exactly200Characters_PassesValidation()
    {
        var endpoint = ValidEndpoint();
        endpoint.Name = new string('a', 200);

        var result = Validate(endpoint);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Name_201Characters_FailsValidation()
    {
        var endpoint = ValidEndpoint();
        endpoint.Name = new string('a', 201);

        var result = Validate(endpoint);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(MonitoredEndpoint.Name));
    }

    // ── Url ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Url_Empty_FailsWithRequiredMessage()
    {
        var endpoint = ValidEndpoint();
        endpoint.Url = string.Empty;

        var result = Validate(endpoint);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(MonitoredEndpoint.Url)
                                            && e.ErrorMessage.Contains("required"));
    }

    [Fact]
    public void Url_Exactly2048Characters_PassesValidation()
    {
        // Construct a URL that is exactly 2048 characters long
        var padding = new string('a', 2048 - "https://x.com/".Length);
        var endpoint = ValidEndpoint();
        endpoint.Url = $"https://x.com/{padding}";

        var result = Validate(endpoint);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Url_Over2048Characters_FailsValidation()
    {
        var padding = new string('a', 2049 - "https://x.com/".Length);
        var endpoint = ValidEndpoint();
        endpoint.Url = $"https://x.com/{padding}";

        var result = Validate(endpoint);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(MonitoredEndpoint.Url));
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("https://example.com")]
    [InlineData("https://sub.domain.example.com/path?q=1")]
    public void Url_ValidHttpOrHttps_PassesValidation(string url)
    {
        var endpoint = ValidEndpoint();
        endpoint.Url = url;

        var result = Validate(endpoint);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("not-a-url")]
    [InlineData("//relative-url")]
    public void Url_NonHttpSchemeOrRelative_FailsValidation(string url)
    {
        var endpoint = ValidEndpoint();
        endpoint.Url = url;

        var result = Validate(endpoint);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(MonitoredEndpoint.Url)
                                            && e.ErrorMessage.Contains("HTTP or HTTPS"));
    }

    // ── CheckIntervalSeconds ─────────────────────────────────────────────────

    [Theory]
    [InlineData(10)]    // lower boundary
    [InlineData(3600)]  // upper boundary
    [InlineData(60)]    // typical value
    public void CheckIntervalSeconds_BoundaryValues_PassesValidation(int value)
    {
        var endpoint = ValidEndpoint();
        endpoint.CheckIntervalSeconds = value;
        // TimeoutSeconds must satisfy: 1 ≤ timeout ≤ 60 AND timeout < CheckIntervalSeconds
        endpoint.TimeoutSeconds = Math.Min(value - 1, 60);

        var result = Validate(endpoint);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(9)]     // one below lower boundary
    [InlineData(3601)]  // one above upper boundary
    public void CheckIntervalSeconds_OutOfRange_FailsValidation(int value)
    {
        var endpoint = ValidEndpoint();
        endpoint.CheckIntervalSeconds = value;

        var result = Validate(endpoint);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(MonitoredEndpoint.CheckIntervalSeconds));
    }

    // ── ExpectedStatusCode ───────────────────────────────────────────────────

    [Theory]
    [InlineData(100)]
    [InlineData(200)]
    [InlineData(599)]
    public void ExpectedStatusCode_BoundaryValues_PassesValidation(int code)
    {
        var endpoint = ValidEndpoint();
        endpoint.ExpectedStatusCode = code;

        var result = Validate(endpoint);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(99)]
    [InlineData(600)]
    public void ExpectedStatusCode_OutOfRange_FailsValidation(int code)
    {
        var endpoint = ValidEndpoint();
        endpoint.ExpectedStatusCode = code;

        var result = Validate(endpoint);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(MonitoredEndpoint.ExpectedStatusCode));
    }

    // ── TimeoutSeconds ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(1)]   // lower boundary
    [InlineData(60)]  // upper boundary
    public void TimeoutSeconds_BoundaryValues_PassesValidation(int timeout)
    {
        var endpoint = ValidEndpoint();
        endpoint.TimeoutSeconds = timeout;
        // CheckIntervalSeconds must be > TimeoutSeconds AND within its own valid range (10-3600).
        // Use max(timeout + 1, 10) to satisfy both constraints.
        endpoint.CheckIntervalSeconds = Math.Max(timeout + 1, 10);

        var result = Validate(endpoint);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void TimeoutSeconds_Zero_FailsValidation()
    {
        var endpoint = ValidEndpoint();
        endpoint.TimeoutSeconds = 0;

        var result = Validate(endpoint);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(MonitoredEndpoint.TimeoutSeconds));
    }

    [Fact]
    public void TimeoutSeconds_61_FailsValidation()
    {
        var endpoint = ValidEndpoint();
        endpoint.TimeoutSeconds = 61;
        endpoint.CheckIntervalSeconds = 3600; // avoid triggering less-than rule

        var result = Validate(endpoint);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(MonitoredEndpoint.TimeoutSeconds));
    }

    [Fact]
    public void TimeoutSeconds_EqualToCheckInterval_FailsWithLessThanMessage()
    {
        var endpoint = ValidEndpoint();
        endpoint.CheckIntervalSeconds = 30;
        endpoint.TimeoutSeconds = 30; // equal, not less-than

        var result = Validate(endpoint);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(MonitoredEndpoint.TimeoutSeconds)
                                            && e.ErrorMessage.Contains("less than the check interval"));
    }

    [Fact]
    public void TimeoutSeconds_GreaterThanCheckInterval_FailsWithLessThanMessage()
    {
        var endpoint = ValidEndpoint();
        endpoint.CheckIntervalSeconds = 30;
        endpoint.TimeoutSeconds = 31;

        var result = Validate(endpoint);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(MonitoredEndpoint.TimeoutSeconds)
                                            && e.ErrorMessage.Contains("less than the check interval"));
    }

    // ── RetryCount ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(2)]
    public void RetryCount_BoundaryValues_PassesValidation(int retryCount)
    {
        var endpoint = ValidEndpoint();
        endpoint.RetryCount = retryCount;

        var result = Validate(endpoint);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void RetryCount_OutOfRange_FailsValidation(int retryCount)
    {
        var endpoint = ValidEndpoint();
        endpoint.RetryCount = retryCount;

        var result = Validate(endpoint);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(MonitoredEndpoint.RetryCount));
    }

    // ── SortOrder ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(9999)]
    [InlineData(500)]
    public void SortOrder_BoundaryValues_PassesValidation(int sortOrder)
    {
        var endpoint = ValidEndpoint();
        endpoint.SortOrder = sortOrder;

        var result = Validate(endpoint);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10000)]
    public void SortOrder_OutOfRange_FailsValidation(int sortOrder)
    {
        var endpoint = ValidEndpoint();
        endpoint.SortOrder = sortOrder;

        var result = Validate(endpoint);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(MonitoredEndpoint.SortOrder));
    }

    // ── Group ────────────────────────────────────────────────────────────────

    [Fact]
    public void Group_Null_PassesValidation()
    {
        var endpoint = ValidEndpoint();
        endpoint.Group = null;

        var result = Validate(endpoint);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Group_Exactly100Characters_PassesValidation()
    {
        var endpoint = ValidEndpoint();
        endpoint.Group = new string('g', 100);

        var result = Validate(endpoint);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Group_101Characters_FailsValidation()
    {
        var endpoint = ValidEndpoint();
        endpoint.Group = new string('g', 101);

        var result = Validate(endpoint);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(MonitoredEndpoint.Group));
    }

    // ── ExpectedBodyMatch ────────────────────────────────────────────────────

    [Fact]
    public void ExpectedBodyMatch_Null_PassesValidation()
    {
        var endpoint = ValidEndpoint();
        endpoint.ExpectedBodyMatch = null;

        var result = Validate(endpoint);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ExpectedBodyMatch_EmptyString_PassesValidation()
    {
        var endpoint = ValidEndpoint();
        endpoint.ExpectedBodyMatch = string.Empty;

        var result = Validate(endpoint);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ExpectedBodyMatch_ValidRegex_PassesValidation()
    {
        var endpoint = ValidEndpoint();
        endpoint.ExpectedBodyMatch = @"^healthy$";

        var result = Validate(endpoint);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ExpectedBodyMatch_InvalidRegex_FailsValidation()
    {
        var endpoint = ValidEndpoint();
        endpoint.ExpectedBodyMatch = "[unclosed bracket";

        var result = Validate(endpoint);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(MonitoredEndpoint.ExpectedBodyMatch)
                                            && e.ErrorMessage.Contains("valid regular expression"));
    }

    [Fact]
    public void ExpectedBodyMatch_Exactly1000Characters_PassesValidation()
    {
        var endpoint = ValidEndpoint();
        endpoint.ExpectedBodyMatch = new string('a', 1000);

        var result = Validate(endpoint);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ExpectedBodyMatch_1001Characters_FailsValidation()
    {
        var endpoint = ValidEndpoint();
        endpoint.ExpectedBodyMatch = new string('a', 1001);

        var result = Validate(endpoint);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(MonitoredEndpoint.ExpectedBodyMatch));
    }
}
