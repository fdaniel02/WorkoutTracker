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
    public int WorkoutTypeId { get; set; }
    public WorkoutType? WorkoutType { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime FinishedAtUtc { get; set; }
}
