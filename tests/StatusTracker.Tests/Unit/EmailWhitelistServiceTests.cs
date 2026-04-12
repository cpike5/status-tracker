using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StatusTracker.Infrastructure;
using StatusTracker.Services;

namespace StatusTracker.Tests.Unit;

/// <summary>
/// Tests for EmailWhitelistService.IsAllowed — covers case-insensitive matching,
/// whitelist parsing (comma-separated), empty/whitespace input, and unknown addresses.
/// </summary>
[Trait("Category", "Unit")]
public class EmailWhitelistServiceTests
{
    // ── Helpers ─────────────────────────────────────────────────────────────

    private static EmailWhitelistService CreateService(string allowedEmails)
    {
        var options = Substitute.For<IOptions<AuthOptions>>();
        options.Value.Returns(new AuthOptions { AllowedEmails = allowedEmails });
        return new EmailWhitelistService(options);
    }

    // ── IsAllowed ────────────────────────────────────────────────────────────

    [Fact]
    public void IsAllowed_ExactMatch_ReturnsTrue()
    {
        var sut = CreateService("user@example.com");

        var result = sut.IsAllowed("user@example.com");

        result.Should().BeTrue();
    }

    [Fact]
    public void IsAllowed_UpperCaseInput_ReturnsTrue()
    {
        // Case-insensitive: stored in lower-case, input in upper-case
        var sut = CreateService("user@example.com");

        var result = sut.IsAllowed("USER@EXAMPLE.COM");

        result.Should().BeTrue();
    }

    [Fact]
    public void IsAllowed_MixedCaseWhitelistEntry_ReturnsTrue()
    {
        // Whitelist entry stored with mixed case
        var sut = CreateService("Admin@Domain.ORG");

        var result = sut.IsAllowed("admin@domain.org");

        result.Should().BeTrue();
    }

    [Fact]
    public void IsAllowed_UnknownEmail_ReturnsFalse()
    {
        var sut = CreateService("allowed@example.com");

        var result = sut.IsAllowed("other@example.com");

        result.Should().BeFalse();
    }

    [Fact]
    public void IsAllowed_EmptyString_ReturnsFalse()
    {
        var sut = CreateService("user@example.com");

        var result = sut.IsAllowed(string.Empty);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsAllowed_WhitespaceOnly_ReturnsFalse()
    {
        var sut = CreateService("user@example.com");

        var result = sut.IsAllowed("   ");

        result.Should().BeFalse();
    }

    [Fact]
    public void IsAllowed_MultipleEntriesInWhitelist_MatchesEachEntry()
    {
        var sut = CreateService("alice@example.com,bob@example.com,carol@example.com");

        sut.IsAllowed("alice@example.com").Should().BeTrue();
        sut.IsAllowed("bob@example.com").Should().BeTrue();
        sut.IsAllowed("carol@example.com").Should().BeTrue();
    }

    [Fact]
    public void IsAllowed_WhitelistWithSpacesAroundCommas_TrimsAndMatches()
    {
        // AuthOptions.GetAllowedEmailList uses TrimEntries, so spaces are stripped
        var sut = CreateService("  alice@example.com , bob@example.com  ");

        sut.IsAllowed("alice@example.com").Should().BeTrue();
        sut.IsAllowed("bob@example.com").Should().BeTrue();
    }

    [Fact]
    public void IsAllowed_EmptyWhitelist_AlwaysReturnsFalse()
    {
        var sut = CreateService(string.Empty);

        var result = sut.IsAllowed("anyone@example.com");

        result.Should().BeFalse();
    }

    [Fact]
    public void IsAllowed_PartialEmailMatch_ReturnsFalse()
    {
        // "user" is not the same as "user@example.com"
        var sut = CreateService("user@example.com");

        var result = sut.IsAllowed("user");

        result.Should().BeFalse();
    }
}
