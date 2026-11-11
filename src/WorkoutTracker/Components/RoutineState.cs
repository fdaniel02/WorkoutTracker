using Microsoft.JSInterop;

namespace WorkoutTracker.Components;

public class RoutineState : TimedState
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int RoutineId { get; set; }
    public DateTime StartedUtc { get; set; }
    public HashSet<int> DoneItemIds { get; set; } = [];

    public static RoutineState Start(int routineId, DateTime nowUtc) =>
        new() { RoutineId = routineId, StartedUtc = nowUtc, RunningSinceUtc = nowUtc };
}

public class RoutineStateStore(IJSRuntime js) : LocalStateStore<RoutineState>(js, "routineState");
