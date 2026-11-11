using Microsoft.EntityFrameworkCore;
using WorkoutTracker.Data;

namespace WorkoutTracker.Tests.Data;

public sealed class WorkoutQueriesTests : IDisposable
{
    readonly TestDatabase database = new();

    [Fact]
    public async Task StartTimesSince_returns_only_this_types_workouts_from_the_given_moment()
    {
        var pushUps = await database.AddWorkoutTypeAsync("Push-ups");
        var squats = await database.AddWorkoutTypeAsync("Squats");
        var since = new DateTime(2026, 6, 28, 22, 0, 0, DateTimeKind.Utc);
        await database.AddWorkoutAsync(pushUps.Id, since.AddSeconds(-1));
        await database.AddWorkoutAsync(pushUps.Id, since);
        await database.AddWorkoutAsync(pushUps.Id, since.AddDays(30));
        await database.AddWorkoutAsync(squats.Id, since.AddDays(1));

        await using var db = database.CreateDbContext();
        var times = await db.Workouts.StartTimesSince(pushUps.Id, since).OrderBy(t => t).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal([since, since.AddDays(30)], times);
    }

    [Fact]
    public async Task StartTimesSince_returns_only_this_routines_runs_from_the_given_moment()
    {
        var type = await database.AddWorkoutTypeAsync("Push-ups");
        var morning = await database.AddRoutineAsync("Morning", (type.Id, 3, null));
        var evening = await database.AddRoutineAsync("Evening", (type.Id, 3, null));
        var since = new DateTime(2026, 6, 28, 22, 0, 0, DateTimeKind.Utc);
        await database.AddRoutineRunAsync(morning.Id, since.AddSeconds(-1));
        await database.AddRoutineRunAsync(morning.Id, since);
        await database.AddRoutineRunAsync(morning.Id, since.AddDays(30));
        await database.AddRoutineRunAsync(evening.Id, since.AddDays(1));

        await using var db = database.CreateDbContext();
        var times = await db.RoutineRuns.StartTimesSince(morning.Id, since).OrderBy(t => t).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal([since, since.AddDays(30)], times);
    }

    public void Dispose() => database.Dispose();
}
