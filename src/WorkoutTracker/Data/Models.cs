namespace WorkoutTracker.Data;

public class WorkoutType
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<Workout> Workouts { get; set; } = [];
}

public class Workout
{
    public int Id { get; set; }
    // Created by the app when the workout starts, so an insert that EF repeats after a lost commit confirmation
    // hits the unique index instead of storing the workout twice.
    public Guid ClientId { get; set; }
    public int WorkoutTypeId { get; set; }
    public WorkoutType? WorkoutType { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime FinishedAtUtc { get; set; }
}
