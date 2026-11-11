using Microsoft.EntityFrameworkCore;
using WorkoutTracker.Data;

namespace WorkoutTracker.Components;

public record RoutineItemView(int Id, int WorkoutTypeId, string Name, string? Target)
{
    public static async Task<List<RoutineItemView>> LoadAsync(AppDbContext db, int routineId)
    {
        var rows = await db.RoutineItems
            .Where(i => i.RoutineId == routineId)
            .OrderBy(i => i.Position).ThenBy(i => i.Id)
            .Select(i => new { i.Id, i.WorkoutTypeId, i.WorkoutType!.Name, i.ProposedSets, i.ProposedReps })
            .ToListAsync();
        return [.. rows.Select(r => new RoutineItemView(r.Id, r.WorkoutTypeId, r.Name, RoutineTarget.Format(r.ProposedSets, r.ProposedReps)))];
    }
}
