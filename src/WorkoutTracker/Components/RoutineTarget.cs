namespace WorkoutTracker.Components;

public static class RoutineTarget
{
    public static string? Format(int? sets, string? reps) => (sets, string.IsNullOrWhiteSpace(reps) ? null : reps.Trim()) switch
    {
        (null, null) => null,
        ({ } s, null) => s == 1 ? "1 set" : $"{s} sets",
        (null, { } r) => r,
        ({ } s, { } r) => $"{s} × {r}"
    };
}
