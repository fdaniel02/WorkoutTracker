using Microsoft.EntityFrameworkCore;

namespace WorkoutTracker.Data;

public static class WorkoutCommands
{
    // Returns true if the workout was already stored: EF repeated the insert after its commit confirmation was lost.
    public static async Task<bool> SaveWorkoutAsync(this AppDbContext db, Workout workout, string typeName, int reps,
        CancellationToken cancellationToken = default)
    {
        using var activity = Telemetry.ActivitySource.StartActivity("SaveWorkout");
        activity?.SetTag("workouttracker.workout_type", typeName);
        activity?.SetTag("workouttracker.reps", reps);
        var alreadySaved = await AddAsync(db, workout, cancellationToken);
        activity?.SetTag("workouttracker.already_saved", alreadySaved);
        return alreadySaved;
    }

    static async Task<bool> AddAsync(AppDbContext db, Workout workout, CancellationToken cancellationToken)
    {
        db.Workouts.Add(workout);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return false;
        }
        // ClientId is the only unique index on Workouts.
        catch (DbUpdateException ex) when (SqlErrors.IsUniqueViolation(ex))
        {
            return true;
        }
    }
}
