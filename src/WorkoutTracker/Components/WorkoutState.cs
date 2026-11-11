using Microsoft.JSInterop;

namespace WorkoutTracker.Components;

public class WorkoutState : TimedState
{
    // A workout saved before ids existed has none in its JSON, so it gets a new one here when it's loaded.
    public Guid Id { get; set; } = Guid.NewGuid();
    public int TypeId { get; set; }
    public DateTime StartedUtc { get; set; }
    public int Reps { get; set; }
    public bool Started { get; set; }
    public bool Resting { get; set; }
    // Set when the workout was started from the routine in progress.
    public int? RoutineItemId { get; set; }

    public override void ToggleTimer(DateTime nowUtc)
    {
        base.ToggleTimer(nowUtc);
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
}

public class WorkoutStateStore(IJSRuntime js) : LocalStateStore<WorkoutState>(js, "workoutState");
