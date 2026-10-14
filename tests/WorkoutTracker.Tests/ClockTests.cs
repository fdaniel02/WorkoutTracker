using WorkoutTracker.Data;

namespace WorkoutTracker.Tests;

public class ClockTests
{
    readonly Clock clock = new(TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam"));

    [Theory]
    // Summer time (UTC+2): 22:30 UTC is already the next day in Amsterdam.
    [InlineData("2026-10-13T22:30:00", "2026-10-14")]
    [InlineData("2026-10-13T21:59:59", "2026-10-13")]
    // Winter time (UTC+1).
    [InlineData("2026-01-13T23:30:00", "2026-01-14")]
    [InlineData("2026-01-13T22:59:59", "2026-01-13")]
    public void ToLocalDate_uses_the_configured_time_zone(string utc, string expected)
    {
        var date = clock.ToLocalDate(DateTime.Parse(utc));

        Assert.Equal(DateOnly.Parse(expected), date);
    }

    [Fact]
    public void ToLocalDate_treats_unspecified_kind_as_utc()
    {
        // EF Core returns DateTime values from SQL Server with Kind = Unspecified.
        var fromDb = new DateTime(2026, 10, 13, 22, 30, 0, DateTimeKind.Unspecified);

        Assert.Equal(new DateOnly(2026, 10, 14), clock.ToLocalDate(fromDb));
    }
}
