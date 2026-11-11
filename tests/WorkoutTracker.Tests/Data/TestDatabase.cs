using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using WorkoutTracker.Data;

namespace WorkoutTracker.Tests.Data;

// SQLite in memory: fast, but not SQL Server. Collation (case sensitivity) and query translation differ,
// so tests about database behaviour itself need a real SQL Server.
public sealed class TestDatabase : IDbContextFactory<AppDbContext>, IDisposable
{
    // The in-memory database exists only while this connection is open.
    readonly SqliteConnection connection = new("DataSource=:memory:");
    readonly DbContextOptions<AppDbContext> appOptions;
    readonly DbContextOptions<AppDbContext> testOptions;

    // The interceptors apply only to contexts the app creates, not to the test's own setup and checks.
    public TestDatabase(params IInterceptor[] interceptors)
    {
        connection.Open();
        testOptions = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        appOptions = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).AddInterceptors(interceptors).Options;
        using var db = new AppDbContext(testOptions);
        db.Database.EnsureCreated();
    }

    public AppDbContext CreateDbContext() => new(appOptions);

    public async Task<WorkoutType> AddWorkoutTypeAsync(string name, bool favorite = false)
    {
        await using var db = new AppDbContext(testOptions);
        var type = new WorkoutType { Name = name, IsFavorite = favorite };
        db.WorkoutTypes.Add(type);
        await db.SaveChangesAsync();
        return type;
    }

    public async Task AddWorkoutAsync(int workoutTypeId, DateTime startedAtUtc)
    {
        await using var db = new AppDbContext(testOptions);
        db.Workouts.Add(new Workout { ClientId = Guid.NewGuid(), WorkoutTypeId = workoutTypeId, StartedAtUtc = startedAtUtc, FinishedAtUtc = startedAtUtc.AddMinutes(10) });
        await db.SaveChangesAsync();
    }

    public async Task<bool> IsFavoriteAsync(int workoutTypeId)
    {
        await using var db = new AppDbContext(testOptions);
        return await db.WorkoutTypes.Where(t => t.Id == workoutTypeId).Select(t => t.IsFavorite).SingleAsync();
    }

    public async Task<int> CountWorkoutsAsync()
    {
        await using var db = new AppDbContext(testOptions);
        return await db.Workouts.CountAsync();
    }

    public async Task<Routine> AddRoutineAsync(string name, params (int TypeId, int? Sets, string? Reps)[] items)
    {
        await using var db = new AppDbContext(testOptions);
        var routine = new Routine
        {
            Name = name,
            Items = [.. items.Select((item, i) => new RoutineItem
            {
                WorkoutTypeId = item.TypeId, Position = i, ProposedSets = item.Sets, ProposedReps = item.Reps
            })]
        };
        db.Routines.Add(routine);
        await db.SaveChangesAsync();
        return routine;
    }

    public async Task AddRoutineRunAsync(int routineId, DateTime startedAtUtc)
    {
        await using var db = new AppDbContext(testOptions);
        db.RoutineRuns.Add(new RoutineRun { ClientId = Guid.NewGuid(), RoutineId = routineId, StartedAtUtc = startedAtUtc, FinishedAtUtc = startedAtUtc.AddMinutes(30) });
        await db.SaveChangesAsync();
    }

    public async Task<List<RoutineRun>> RoutineRunsAsync()
    {
        await using var db = new AppDbContext(testOptions);
        return await db.RoutineRuns.ToListAsync();
    }

    public void Dispose() => connection.Dispose();
}
