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
    public void First_toggle_starts_the_timer()
    {
        var state = new WorkoutState();

        state.ToggleTimer(Now);

        Assert.True(state.Started);
        Assert.True(state.Running);
        Assert.Equal(TimeSpan.FromSeconds(10), state.Elapsed(Now.AddSeconds(10)));
    }

    [Fact]
    public void Pause_keeps_the_elapsed_time_and_resume_continues_from_it()
    {
        var state = new WorkoutState();
        state.ToggleTimer(Now);

        state.ToggleTimer(Now.AddSeconds(30));

        Assert.False(state.Running);
        Assert.Equal(TimeSpan.FromSeconds(30), state.Elapsed(Now.AddMinutes(5)));

        state.ToggleTimer(Now.AddMinutes(5));

        Assert.True(state.Running);
        Assert.Equal(TimeSpan.FromSeconds(40), state.Elapsed(Now.AddMinutes(5).AddSeconds(10)));
    }

    [Fact]
    public void Rep_starts_a_rest_timed_from_zero()
    {
        var state = new WorkoutState();
        state.ToggleTimer(Now);

        state.AddRep(Now.AddSeconds(45));

        Assert.Equal(1, state.Reps);
        Assert.True(state.Resting);
        Assert.True(state.Running);
        Assert.Equal(TimeSpan.FromSeconds(5), state.Elapsed(Now.AddSeconds(50)));
    }

    [Fact]
    public void Rep_while_paused_starts_the_rest_running()
    {
        var state = new WorkoutState { Started = true, AccumulatedBeforePause = TimeSpan.FromSeconds(20) };

        state.AddRep(Now);

        Assert.True(state.Running);
        Assert.Equal(TimeSpan.Zero, state.Elapsed(Now));
    }

    [Fact]
    public void Ending_the_rest_times_the_next_set_from_zero()
    {
        var state = new WorkoutState();
        state.ToggleTimer(Now);
        state.AddRep(Now.AddSeconds(45));

        state.EndRest(Now.AddSeconds(105));

        Assert.False(state.Resting);
        Assert.Equal(1, state.Reps);
        Assert.Equal(TimeSpan.FromSeconds(3), state.Elapsed(Now.AddSeconds(108)));
    }

    [Fact]
    public void Stop_freezes_the_elapsed_time()
    {
        var state = new WorkoutState();
        state.ToggleTimer(Now);

        state.Stop(Now.AddSeconds(75));

        Assert.False(state.Running);
        Assert.Equal(TimeSpan.FromSeconds(75), state.Elapsed(Now.AddHours(1)));
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
