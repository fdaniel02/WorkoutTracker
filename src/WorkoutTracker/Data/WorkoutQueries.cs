namespace WorkoutTracker.Data;

public static class WorkoutQueries
{
    public static IQueryable<DateTime> StartTimesSince(this IQueryable<Workout> workouts, int workoutTypeId, DateTime sinceUtc) =>
        workouts
            .Where(w => w.WorkoutTypeId == workoutTypeId && w.StartedAtUtc >= sinceUtc)
            .Select(w => w.StartedAtUtc);

    public static IQueryable<DateTime> StartTimesSince(this IQueryable<RoutineRun> runs, int routineId, DateTime sinceUtc) =>
        runs
            .Where(r => r.RoutineId == routineId && r.StartedAtUtc >= sinceUtc)
            .Select(r => r.StartedAtUtc);
}
