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

    public async Task<WorkoutType> AddWorkoutTypeAsync(string name)
    {
        await using var db = new AppDbContext(testOptions);
        var type = new WorkoutType { Name = name };
        db.WorkoutTypes.Add(type);
        await db.SaveChangesAsync();
        return type;
    }

    public async Task<int> CountWorkoutsAsync()
    {
        await using var db = new AppDbContext(testOptions);
        return await db.Workouts.CountAsync();
    }

    public void Dispose() => connection.Dispose();
}
