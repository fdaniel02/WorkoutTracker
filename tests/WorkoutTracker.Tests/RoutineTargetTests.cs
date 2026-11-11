using WorkoutTracker.Components;

namespace WorkoutTracker.Tests;

public class RoutineTargetTests
{
    [Theory]
    [InlineData(3, "8-12 per side", "3 × 8-12 per side")]
    [InlineData(3, null, "3 sets")]
    [InlineData(1, null, "1 set")]
    [InlineData(null, "30 s", "30 s")]
    [InlineData(2, "  15  ", "2 × 15")]
    [InlineData(2, " ", "2 sets")]
    [InlineData(null, null, null)]
    [InlineData(null, "", null)]
    public void Shows_whatever_part_of_the_target_is_set(int? sets, string? reps, string? expected) =>
        Assert.Equal(expected, RoutineTarget.Format(sets, reps));
}
