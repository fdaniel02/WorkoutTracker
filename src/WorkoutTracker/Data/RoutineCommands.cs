using Microsoft.EntityFrameworkCore;

namespace WorkoutTracker.Data;

public static class RoutineCommands
{
    // Returns true if the run was already stored: EF repeated the insert after its commit confirmation was lost.
    public static async Task<bool> SaveRoutineRunAsync(this AppDbContext db, RoutineRun run, string routineName,
        CancellationToken cancellationToken = default)
    {
        using var activity = Telemetry.ActivitySource.StartActivity("SaveRoutineRun");
        activity?.SetTag("workouttracker.routine", routineName);
        var alreadySaved = await AddAsync(db, run, cancellationToken);
        activity?.SetTag("workouttracker.already_saved", alreadySaved);
        return alreadySaved;
    }

    static async Task<bool> AddAsync(AppDbContext db, RoutineRun run, CancellationToken cancellationToken)
    {
        db.RoutineRuns.Add(run);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return false;
        }
        // ClientId is the only unique index on RoutineRuns.
        catch (DbUpdateException ex) when (SqlErrors.IsUniqueViolation(ex))
        {
            return true;
        }
    }
}
