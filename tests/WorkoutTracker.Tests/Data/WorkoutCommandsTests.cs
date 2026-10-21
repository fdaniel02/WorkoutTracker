using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using WorkoutTracker.Data;

namespace WorkoutTracker.Tests.Data;

// SQL Server, not SQLite: the unique index error and EF's retrying execution strategy are what's being tested.
[Collection("SqlServer")]
public sealed class WorkoutCommandsTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    static readonly DateTime Started = new(2026, 10, 21, 7, 0, 0, DateTimeKind.Utc);

    readonly string connectionString = sqlServer.ConnectionString(SqlServerFixture.NewDatabaseName());
    int typeId;

    // The same retry settings as the app's pages.
    AppDbContext NewContext(params IInterceptor[] interceptors)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Default"] = connectionString })
            .Build();
        return new(new DbContextOptionsBuilder<AppDbContext>()
            .UseAppDatabase(configuration, retryOnFailure: true).AddInterceptors(interceptors).Options);
    }

    public async ValueTask InitializeAsync()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var type = new WorkoutType { Name = "Push ups" };
        db.WorkoutTypes.Add(type);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        typeId = type.Id;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    Workout NewWorkout(Guid clientId) =>
        new() { ClientId = clientId, WorkoutTypeId = typeId, StartedAtUtc = Started, FinishedAtUtc = Started.AddMinutes(10) };

    async Task<List<Guid>> ClientIdsAsync()
    {
        await using var db = NewContext();
        return await db.Workouts.Select(w => w.ClientId).ToListAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task New_workout_is_saved()
    {
        var clientId = Guid.NewGuid();
        await using var db = NewContext();

        Assert.False(await db.SaveWorkoutAsync(NewWorkout(clientId), "Push ups", 3, TestContext.Current.CancellationToken));

        Assert.Equal([clientId], await ClientIdsAsync());
    }

    [Fact]
    public async Task Insert_repeated_after_a_lost_commit_confirmation_is_stored_once()
    {
        var clientId = Guid.NewGuid();
        var lostConfirmation = new LostCommitConfirmationInterceptor();
        await using var db = NewContext(lostConfirmation);

        var alreadySaved = await db.SaveWorkoutAsync(NewWorkout(clientId), "Push ups", 3, TestContext.Current.CancellationToken);

        Assert.True(lostConfirmation.Failed);
        Assert.True(alreadySaved);
        Assert.Equal([clientId], await ClientIdsAsync());
    }

    [Fact]
    public async Task Workouts_with_the_same_times_but_different_ids_are_both_saved()
    {
        await using (var first = NewContext())
            await first.SaveWorkoutAsync(NewWorkout(Guid.NewGuid()), "Push ups", 3, TestContext.Current.CancellationToken);
        await using var db = NewContext();

        Assert.False(await db.SaveWorkoutAsync(NewWorkout(Guid.NewGuid()), "Push ups", 3, TestContext.Current.CancellationToken));

        Assert.Equal(2, (await ClientIdsAsync()).Count);
    }

    [Fact]
    public async Task Save_records_a_span_with_whether_it_was_already_saved()
    {
        using var trace = new TestTrace();
        using var spans = new ActivityCollector(a => a.OperationName == "SaveWorkout" && a.TraceId == trace.Id);
        await using var db = NewContext(new LostCommitConfirmationInterceptor());

        await db.SaveWorkoutAsync(NewWorkout(Guid.NewGuid()), "Push ups", 3, TestContext.Current.CancellationToken);

        var span = Assert.Single(spans.Stopped);
        Assert.Equal("Push ups", span.GetTagItem("workouttracker.workout_type"));
        Assert.Equal(3, span.GetTagItem("workouttracker.reps"));
        Assert.True(span.GetTagItem("workouttracker.already_saved") is true);
    }
}
