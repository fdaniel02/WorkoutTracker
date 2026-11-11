using System.Diagnostics.Metrics;

namespace WorkoutTracker;

public class WorkoutMetrics
{
    readonly Counter<long> workoutsSaved;
    readonly Counter<long> routinesSaved;

    public WorkoutMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(Telemetry.Name);
        workoutsSaved = meter.CreateCounter<long>("workouttracker.workouts.saved", "{workout}", "Workouts saved with Finish");
        routinesSaved = meter.CreateCounter<long>("workouttracker.routines.saved", "{routine}", "Routines saved with Finish routine");
    }

    public void WorkoutSaved(string workoutType) =>
        workoutsSaved.Add(1, new KeyValuePair<string, object?>("workouttracker.workout_type", workoutType));

    public void RoutineSaved(string routine) =>
        routinesSaved.Add(1, new KeyValuePair<string, object?>("workouttracker.routine", routine));
}
