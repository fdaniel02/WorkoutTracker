using Microsoft.EntityFrameworkCore;
using WorkoutTracker.Data;

namespace WorkoutTracker.Tests.Data;

// What the model asks of PostgreSQL itself: the case-insensitive collation and UTC timestamps.
[Collection("Postgres")]
public sealed class PostgresModelTests(PostgresFixture postgres) : IAsyncLifetime
{
    readonly string connectionString = postgres.ConnectionString(PostgresFixture.NewDatabaseName());

    AppDbContext NewContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);

    public async ValueTask InitializeAsync()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Workout_type_names_differing_only_in_case_are_duplicates()
    {
        await using (var first = NewContext())
        {
            first.WorkoutTypes.Add(new WorkoutType { Name = "Push ups" });
            await first.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await using var db = NewContext();
        db.WorkoutTypes.Add(new WorkoutType { Name = "PUSH UPS" });

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.True(SqlErrors.IsUniqueViolation(error));
    }

    [Fact]
    public async Task Routine_names_differing_only_in_case_are_duplicates()
    {
        await using (var first = NewContext())
        {
            first.Routines.Add(new Routine { Name = "Legs and feet" });
            await first.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await using var db = NewContext();
        db.Routines.Add(new Routine { Name = "legs and FEET" });

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.True(SqlErrors.IsUniqueViolation(error));
    }

    [Fact]
    public async Task Lookup_by_name_ignores_case()
    {
        await using var db = NewContext();
        db.WorkoutTypes.Add(new WorkoutType { Name = "Squats" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.True(await db.WorkoutTypes.AnyAsync(t => t.Name == "squats", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Times_without_a_kind_are_stored_as_utc()
    {
        var started = new DateTime(2026, 10, 21, 7, 0, 0, DateTimeKind.Unspecified);
        await using (var db = NewContext())
        {
            var type = new WorkoutType { Name = "Lunges" };
            db.Workouts.Add(new Workout { ClientId = Guid.NewGuid(), WorkoutType = type, StartedAtUtc = started, FinishedAtUtc = started.AddMinutes(10) });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = NewContext();
        var stored = await read.Workouts.Select(w => w.StartedAtUtc).SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(DateTimeKind.Utc, stored.Kind);
        Assert.Equal(started.Ticks, stored.Ticks);
    }
}
