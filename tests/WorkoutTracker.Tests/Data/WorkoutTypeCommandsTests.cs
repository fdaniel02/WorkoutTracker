using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using WorkoutTracker.Data;

namespace WorkoutTracker.Tests.Data;

// SQL Server, not SQLite: the unique index error and the case-insensitive collation are what's being tested.
[Collection("SqlServer")]
public sealed class WorkoutTypeCommandsTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    readonly string connectionString = sqlServer.ConnectionString(SqlServerFixture.NewDatabaseName());

    AppDbContext NewContext(params IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).AddInterceptors(interceptors).Options);

    public async ValueTask InitializeAsync()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    async Task<List<string>> NamesAsync()
    {
        await using var db = NewContext();
        return await db.WorkoutTypes.Select(t => t.Name).ToListAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task New_name_is_added()
    {
        await using var db = NewContext();

        Assert.True(await db.TryAddWorkoutTypeAsync("Push ups", TestContext.Current.CancellationToken));

        Assert.Equal(["Push ups"], await NamesAsync());
    }

    [Fact]
    public async Task Existing_name_in_other_casing_is_not_added()
    {
        await using (var first = NewContext())
            await first.TryAddWorkoutTypeAsync("Push ups", TestContext.Current.CancellationToken);
        await using var db = NewContext();

        Assert.False(await db.TryAddWorkoutTypeAsync("push ups", TestContext.Current.CancellationToken));

        Assert.Equal(["Push ups"], await NamesAsync());
    }

    [Fact]
    public async Task Name_added_by_another_request_after_the_check_is_not_added()
    {
        var otherRequest = new BeforeSaveInterceptor(async () =>
        {
            await using var other = NewContext();
            other.WorkoutTypes.Add(new WorkoutType { Name = "Push ups" });
            await other.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        await using var db = NewContext(otherRequest);

        Assert.False(await db.TryAddWorkoutTypeAsync("Push ups", TestContext.Current.CancellationToken));

        Assert.Equal(["Push ups"], await NamesAsync());
    }

    [Fact]
    public async Task Each_add_records_a_span_with_its_result()
    {
        using var spans = new ActivityCollector(a => a.OperationName == "AddWorkoutType" && Equals(a.GetTagItem("workouttracker.workout_type"), "Lunges"));
        await using var db = NewContext();

        await db.TryAddWorkoutTypeAsync("Lunges", TestContext.Current.CancellationToken);
        await db.TryAddWorkoutTypeAsync("Lunges", TestContext.Current.CancellationToken);

        Assert.Equal([true, false], spans.Stopped.Select(s => s.GetTagItem("workouttracker.added")));
    }

    [Fact]
    public async Task Favorite_is_set_and_cleared_only_on_its_type()
    {
        await using var db = NewContext();
        await db.TryAddWorkoutTypeAsync("Push ups", TestContext.Current.CancellationToken);
        await db.TryAddWorkoutTypeAsync("Squats", TestContext.Current.CancellationToken);
        var pushUps = await db.WorkoutTypes.Where(t => t.Name == "Push ups").Select(t => t.Id).SingleAsync(TestContext.Current.CancellationToken);

        await db.SetFavoriteAsync(pushUps, true, TestContext.Current.CancellationToken);
        Assert.Equal(["Push ups"], await FavoritesAsync());

        await db.SetFavoriteAsync(pushUps, false, TestContext.Current.CancellationToken);
        Assert.Empty(await FavoritesAsync());
    }

    [Fact]
    public async Task Setting_a_favorite_records_a_span()
    {
        await using var db = NewContext();
        await db.TryAddWorkoutTypeAsync("Planks", TestContext.Current.CancellationToken);
        var id = await db.WorkoutTypes.Where(t => t.Name == "Planks").Select(t => t.Id).SingleAsync(TestContext.Current.CancellationToken);
        using var spans = new ActivityCollector(a => a.OperationName == "SetWorkoutTypeFavorite" && Equals(a.GetTagItem("workouttracker.workout_type_id"), id));

        await db.SetFavoriteAsync(id, true, TestContext.Current.CancellationToken);

        var span = Assert.Single(spans.Stopped);
        Assert.Equal(true, span.GetTagItem("workouttracker.favorite"));
    }

    async Task<List<string>> FavoritesAsync()
    {
        await using var db = NewContext();
        return await db.WorkoutTypes.Where(t => t.IsFavorite).Select(t => t.Name).ToListAsync(TestContext.Current.CancellationToken);
    }

    sealed class BeforeSaveInterceptor(Func<Task> beforeSave) : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            await beforeSave();
            return result;
        }
    }
}
