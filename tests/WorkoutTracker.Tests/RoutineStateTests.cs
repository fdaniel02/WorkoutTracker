using System.Text.Json;
using WorkoutTracker.Components;

namespace WorkoutTracker.Tests;

public class RoutineStateTests
{
    static readonly DateTime Now = new(2026, 10, 14, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Start_runs_the_timer_from_now_with_nothing_done()
    {
        var state = RoutineState.Start(7, Now);

        Assert.Equal(7, state.RoutineId);
        Assert.Equal(Now, state.StartedUtc);
        Assert.True(state.Running);
        Assert.Equal(TimeSpan.FromMinutes(2), state.Elapsed(Now.AddMinutes(2)));
        Assert.Empty(state.DoneItemIds);
        Assert.NotEqual(Guid.Empty, state.Id);
    }

    [Fact]
    public void Toggle_pauses_and_resumes()
    {
        var state = RoutineState.Start(7, Now);

        state.ToggleTimer(Now.AddSeconds(30));
        Assert.False(state.Running);
        Assert.Equal(TimeSpan.FromSeconds(30), state.Elapsed(Now.AddHours(1)));

        state.ToggleTimer(Now.AddHours(1));
        Assert.Equal(TimeSpan.FromSeconds(40), state.Elapsed(Now.AddHours(1).AddSeconds(10)));
    }

    [Fact]
    public void Json_round_trip_keeps_all_fields()
    {
        var state = RoutineState.Start(7, Now);
        state.DoneItemIds.Add(3);
        state.DoneItemIds.Add(5);
        state.Stop(Now.AddSeconds(12.5));

        var restored = JsonSerializer.Deserialize<RoutineState>(JsonSerializer.Serialize(state))!;

        Assert.Equivalent(state, restored, strict: true);
    }
}
