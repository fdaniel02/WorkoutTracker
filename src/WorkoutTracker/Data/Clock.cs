namespace WorkoutTracker.Data;

// Converts to calendar dates in the user's time zone, independent of the server's (UTC in Docker).
public class Clock(TimeZoneInfo timeZone)
{
    public DateOnly Today => ToLocalDate(DateTime.UtcNow);

    public DateOnly ToLocalDate(DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZone));
}
