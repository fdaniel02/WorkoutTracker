using System.Diagnostics.Metrics;

namespace WorkoutTracker;

public class WorkoutMetrics
{
    readonly Counter<long> workoutsSaved;

    public WorkoutMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(Telemetry.Name);
        workoutsSaved = meter.CreateCounter<long>("workouttracker.workouts.saved", "{workout}", "Workouts saved with Finish");
    }

    public void WorkoutSaved(string workoutType) =>
        workoutsSaved.Add(1, new KeyValuePair<string, object?>("workouttracker.workout_type", workoutType));
}
