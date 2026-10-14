using System.Text.Json;
using WorkoutTracker.Components;

namespace WorkoutTracker.Tests;

public class WorkoutStateTests
{
    static readonly DateTime Now = new(2026, 10, 14, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Elapsed_while_running_adds_time_since_start()
    {
        var state = new WorkoutState
        {
            AccumulatedBeforePause = TimeSpan.FromSeconds(30),
            RunningSinceUtc = Now.AddSeconds(-45)
        };

        Assert.True(state.Running);
        Assert.Equal(TimeSpan.FromSeconds(75), state.Elapsed(Now));
    }

    [Fact]
    public void Elapsed_while_paused_is_the_accumulated_time()
    {
        var state = new WorkoutState { AccumulatedBeforePause = TimeSpan.FromSeconds(30) };

        Assert.False(state.Running);
        Assert.Equal(TimeSpan.FromSeconds(30), state.Elapsed(Now.AddHours(1)));
    }

    [Fact]
    public void Json_round_trip_keeps_all_fields()
    {
        var state = new WorkoutState
        {
            TypeId = 3,
            StartedUtc = Now.AddMinutes(-10),
            Reps = 4,
            Started = true,
            Resting = true,
            AccumulatedBeforePause = TimeSpan.FromSeconds(12.5),
            RunningSinceUtc = Now
        };

        var restored = JsonSerializer.Deserialize<WorkoutState>(JsonSerializer.Serialize(state))!;

        Assert.Equivalent(state, restored, strict: true);
        Assert.Equal(DateTimeKind.Utc, restored.RunningSinceUtc!.Value.Kind);
    }
}
