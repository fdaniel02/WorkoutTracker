using System.Text.Json;
using Microsoft.JSInterop;

namespace WorkoutTracker.Components;

// The in-progress workout. The timer is stored as timestamps, so the elapsed time
// stays correct after a reload, including the time the phone was locked.
public class WorkoutState
{
    // A workout saved before ids existed has none in its JSON, so it gets a new one here when it's loaded.
    public Guid Id { get; set; } = Guid.NewGuid();
    public int TypeId { get; set; }
    public DateTime StartedUtc { get; set; }
    public int Reps { get; set; }
    public bool Started { get; set; }
    public bool Resting { get; set; }
    public TimeSpan AccumulatedBeforePause { get; set; }
    public DateTime? RunningSinceUtc { get; set; }

    public bool Running => RunningSinceUtc is not null;

    public TimeSpan Elapsed(DateTime nowUtc) =>
        AccumulatedBeforePause + (RunningSinceUtc is { } since ? nowUtc - since : TimeSpan.Zero);

    public void ToggleTimer(DateTime nowUtc)
    {
        if (Running)
            Stop(nowUtc);
        else
            RunningSinceUtc = nowUtc;
        Started = true;
    }

    // Each rep starts a rest period, timed from zero.
    public void AddRep(DateTime nowUtc)
    {
        Reps++;
        RestartTimer(nowUtc);
        Started = true;
        Resting = true;
    }

    // Leaving rest starts the next set, timed from zero.
    public void EndRest(DateTime nowUtc)
    {
        Resting = false;
        RestartTimer(nowUtc);
    }

    public void Stop(DateTime nowUtc)
    {
        AccumulatedBeforePause = Elapsed(nowUtc);
        RunningSinceUtc = null;
    }

    void RestartTimer(DateTime nowUtc)
    {
        AccumulatedBeforePause = TimeSpan.Zero;
        RunningSinceUtc = nowUtc;
    }
}

public class WorkoutStateStore(IJSRuntime js)
{
    public async Task<WorkoutState?> LoadAsync()
    {
        var json = await js.InvokeAsync<string?>("workoutState.load");
        if (json is null) return null;
        try { return JsonSerializer.Deserialize<WorkoutState>(json); }
        catch (JsonException) { return null; }
    }

    public Task SaveAsync(WorkoutState state) =>
        js.InvokeVoidAsync("workoutState.save", JsonSerializer.Serialize(state)).AsTask();

    public Task ClearAsync() => js.InvokeVoidAsync("workoutState.clear").AsTask();
}
