using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using WorkoutTracker.Data;

namespace WorkoutTracker.Tests.Data;

// SQL Server, not SQLite: the unique index error and EF's retrying execution strategy are what's being tested.
[Collection("SqlServer")]
public sealed class RoutineCommandsTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    static readonly DateTime Started = new(2026, 10, 21, 7, 0, 0, DateTimeKind.Utc);

    readonly string connectionString = sqlServer.ConnectionString(SqlServerFixture.NewDatabaseName());
    int routineId;

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
        var type = new WorkoutType { Name = "Calf raise" };
        var routine = new Routine
        {
            Name = "Legs and feet",
            Items = [new RoutineItem { WorkoutType = type, Position = 0, ProposedSets = 3, ProposedReps = "8-12 per side" }]
        };
        db.Routines.Add(routine);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        routineId = routine.Id;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    RoutineRun NewRun(Guid clientId) =>
        new() { ClientId = clientId, RoutineId = routineId, StartedAtUtc = Started, FinishedAtUtc = Started.AddMinutes(30) };

    async Task<List<Guid>> ClientIdsAsync()
    {
        await using var db = NewContext();
        return await db.RoutineRuns.Select(r => r.ClientId).ToListAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task New_run_is_saved()
    {
        var clientId = Guid.NewGuid();
        await using var db = NewContext();

        Assert.False(await db.SaveRoutineRunAsync(NewRun(clientId), "Legs and feet", TestContext.Current.CancellationToken));

        Assert.Equal([clientId], await ClientIdsAsync());
    }

    [Fact]
    public async Task Insert_repeated_after_a_lost_commit_confirmation_is_stored_once()
    {
        var clientId = Guid.NewGuid();
        var lostConfirmation = new LostCommitConfirmationInterceptor("RoutineRuns");
        await using var db = NewContext(lostConfirmation);

        var alreadySaved = await db.SaveRoutineRunAsync(NewRun(clientId), "Legs and feet", TestContext.Current.CancellationToken);

        Assert.True(lostConfirmation.Failed);
        Assert.True(alreadySaved);
        Assert.Equal([clientId], await ClientIdsAsync());
    }

    [Fact]
    public async Task Deleting_a_workout_type_removes_it_from_routines_but_keeps_the_routine()
    {
        await using (var db = NewContext())
            await db.WorkoutTypes.ExecuteDeleteAsync(TestContext.Current.CancellationToken);

        await using var check = NewContext();
        Assert.Equal(0, await check.RoutineItems.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await check.Routines.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Save_records_a_span_with_whether_it_was_already_saved()
    {
        using var trace = new TestTrace();
        using var spans = new ActivityCollector(a => a.OperationName == "SaveRoutineRun" && a.TraceId == trace.Id);
        await using var db = NewContext(new LostCommitConfirmationInterceptor("RoutineRuns"));

        await db.SaveRoutineRunAsync(NewRun(Guid.NewGuid()), "Legs and feet", TestContext.Current.CancellationToken);

        var span = Assert.Single(spans.Stopped);
        Assert.Equal("Legs and feet", span.GetTagItem("workouttracker.routine"));
        Assert.True(span.GetTagItem("workouttracker.already_saved") is true);
    }
}
