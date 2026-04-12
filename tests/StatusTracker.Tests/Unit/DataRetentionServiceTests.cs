using FluentAssertions;
using StatusTracker.Services;

namespace StatusTracker.Tests.Unit;

public class DataRetentionServiceTests
{
    // ── ParseDailySchedule ───────────────────────────────────────────────────

    [Fact]
    public void ParseDailySchedule_ValidCron_ReturnsCorrectHourMinute()
    {
        // "0 2 * * *" — minute=0, hour=2
        var (hour, minute) = DataRetentionService.ParseDailySchedule("0 2 * * *");

        hour.Should().Be(2);
        minute.Should().Be(0);
    }

    [Fact]
    public void ParseDailySchedule_NonZeroMinute_ReturnsCorrectHourMinute()
    {
        // "30 3 * * *" — minute=30, hour=3
        var (hour, minute) = DataRetentionService.ParseDailySchedule("30 3 * * *");

        hour.Should().Be(3);
        minute.Should().Be(30);
    }

    [Fact]
    public void ParseDailySchedule_InvalidCron_ReturnsDefault()
    {
        var (hour, minute) = DataRetentionService.ParseDailySchedule("not-a-cron");

        hour.Should().Be(2);
        minute.Should().Be(0);
    }

    [Fact]
    public void ParseDailySchedule_EmptyString_ReturnsDefault()
    {
        var (hour, minute) = DataRetentionService.ParseDailySchedule(string.Empty);

        hour.Should().Be(2);
        minute.Should().Be(0);
    }

    [Fact]
    public void ParseDailySchedule_OutOfRangeHour_ReturnsDefault()
    {
        var (hour, minute) = DataRetentionService.ParseDailySchedule("0 25 * * *");

        hour.Should().Be(2);
        minute.Should().Be(0);
    }

    [Fact]
    public void ParseDailySchedule_SinglePart_ReturnsDefault()
    {
        var (hour, minute) = DataRetentionService.ParseDailySchedule("2");

        hour.Should().Be(2);
        minute.Should().Be(0);
    }

    [Fact]
    public void ParseDailySchedule_WildcardFields_ReturnsDefault()
    {
        var (hour, minute) = DataRetentionService.ParseDailySchedule("* 2 * * *");

        hour.Should().Be(2);
        minute.Should().Be(0);
    }

    // ── GetNextRunTime ───────────────────────────────────────────────────────

    [Fact]
    public void GetNextRunTime_BeforeScheduledTime_ReturnsSameDay()
    {
        // now is 01:00 UTC, schedule is 02:00 UTC → next run is today at 02:00
        var now = new DateTime(2026, 4, 12, 1, 0, 0, DateTimeKind.Utc);

        var result = DataRetentionService.GetNextRunTime(now, hour: 2, minute: 0);

        result.Should().Be(new DateTime(2026, 4, 12, 2, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void GetNextRunTime_AfterScheduledTime_ReturnsNextDay()
    {
        // now is 03:00 UTC, schedule is 02:00 UTC → next run is tomorrow at 02:00
        var now = new DateTime(2026, 4, 12, 3, 0, 0, DateTimeKind.Utc);

        var result = DataRetentionService.GetNextRunTime(now, hour: 2, minute: 0);

        result.Should().Be(new DateTime(2026, 4, 13, 2, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void GetNextRunTime_ExactlyAtScheduledTime_ReturnsNextDay()
    {
        // now is exactly 02:00 UTC — the scheduled moment has passed (not strictly in the future)
        var now = new DateTime(2026, 4, 12, 2, 0, 0, DateTimeKind.Utc);

        var result = DataRetentionService.GetNextRunTime(now, hour: 2, minute: 0);

        result.Should().Be(new DateTime(2026, 4, 13, 2, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void GetNextRunTime_BeforeNonZeroMinuteSchedule_ReturnsSameDay()
    {
        // now is 03:15 UTC, schedule is 03:30 UTC → same day
        var now = new DateTime(2026, 4, 12, 3, 15, 0, DateTimeKind.Utc);

        var result = DataRetentionService.GetNextRunTime(now, hour: 3, minute: 30);

        result.Should().Be(new DateTime(2026, 4, 12, 3, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void GetNextRunTime_AfterNonZeroMinuteSchedule_ReturnsNextDay()
    {
        // now is 03:45 UTC, schedule is 03:30 UTC → next day
        var now = new DateTime(2026, 4, 12, 3, 45, 0, DateTimeKind.Utc);

        var result = DataRetentionService.GetNextRunTime(now, hour: 3, minute: 30);

        result.Should().Be(new DateTime(2026, 4, 13, 3, 30, 0, DateTimeKind.Utc));
    }
}
