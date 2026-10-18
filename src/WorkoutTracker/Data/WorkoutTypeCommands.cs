using Microsoft.EntityFrameworkCore;

namespace WorkoutTracker.Data;

public static class WorkoutTypeCommands
{
    public static async Task<bool> TryAddWorkoutTypeAsync(this AppDbContext db, string name, CancellationToken cancellationToken = default)
    {
        if (await db.WorkoutTypes.AnyAsync(t => t.Name == name, cancellationToken))
            return false;

        db.WorkoutTypes.Add(new WorkoutType { Name = name });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        // Another request can add the same name between the check and the insert (e.g. a double submit).
        catch (DbUpdateException ex) when (SqlErrors.IsUniqueViolation(ex))
        {
            return false;
        }
    }
}
