using System.Diagnostics.Metrics;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using WorkoutTracker.Components;
using WorkoutTracker.Components.Pages;
using WorkoutTracker.Data;
using WorkoutTracker.Tests.Data;

namespace WorkoutTracker.Tests.Components;

public class RoutineRunPageTests : PageTestContext
{
    Routine routine = null!;
    List<int> itemIds = [];
    List<int> typeIds = [];

    async Task<IRenderedComponent<RoutineRunPage>> OpenAsync(Action<RoutineState>? arrange = null)
    {
        await SeedAsync();
        return Open(arrange);
    }

    async Task SeedAsync()
    {
        var squat = await Database.AddWorkoutTypeAsync("Split squat");
        var calves = await Database.AddWorkoutTypeAsync("Calf raise");
        var plank = await Database.AddWorkoutTypeAsync("Side plank");
        typeIds = [squat.Id, calves.Id, plank.Id];
        routine = await Database.AddRoutineAsync("Legs and feet",
            (squat.Id, 3, "8 per side"), (calves.Id, 3, "8-12 per side"), (plank.Id, null, "30 s"));
        itemIds = [.. routine.Items.Select(i => i.Id)];
    }

    IRenderedComponent<RoutineRunPage> Open(Action<RoutineState>? arrange = null)
    {
        var state = RoutineState.Start(routine.Id, UtcNow.AddMinutes(-5));
        arrange?.Invoke(state);
        SetUpSavedRoutine(state);

        var page = Render<RoutineRunPage>(p => p.Add(x => x.Id, routine.Id));
        page.WaitForElement(".routine-items");
        return page;
    }

    [Fact]
    public async Task Without_this_routine_in_progress_goes_to_the_routine()
    {
        var other = await Database.AddRoutineAsync("Fingers");
        var routineNotStarted = await Database.AddRoutineAsync("Legs");
        SetUpSavedRoutine(RoutineState.Start(other.Id, UtcNow));

        var page = Render<RoutineRunPage>(p => p.Add(x => x.Id, routineNotStarted.Id));

        page.WaitForAssertion(() => Assert.EndsWith($"routine/{routineNotStarted.Id}", Uri));
    }

    [Fact]
    public void Unknown_routine_goes_home()
    {
        var page = Render<RoutineRunPage>(p => p.Add(x => x.Id, 999));

        page.WaitForAssertion(() => Assert.Equal("http://localhost/", Uri));
    }

    [Fact]
    public async Task Shows_the_time_since_start_and_each_workout_with_its_target_and_link()
    {
        var page = await OpenAsync();

        Assert.Equal("05:00", page.Find(".timer").TextContent);
        Assert.Equal("0 / 3 done", page.Find(".progress").TextContent);
        var items = page.FindAll(".routine-item");
        Assert.Equal(["Split squat", "Calf raise", "Side plank"], items.Select(i => i.QuerySelector(".name")!.TextContent));
        Assert.Equal(["3 × 8 per side", "3 × 8-12 per side", "30 s"], items.Select(i => i.QuerySelector(".target")!.TextContent));
        Assert.Equal(
            Enumerable.Range(0, 3).Select(i => $"workout/{typeIds[i]}?item={itemIds[i]}"),
            items.Select(i => i.GetAttribute("href")));
    }

    [Fact]
    public async Task Done_workouts_are_ticked_and_the_first_undone_one_is_next()
    {
        var page = await OpenAsync(s => s.DoneItemIds.Add(itemIds[0]));

        var items = page.FindAll(".routine-item");
        Assert.Equal([true, false, false], items.Select(i => i.ClassList.Contains("done")));
        Assert.Equal(["✓", "", ""], items.Select(i => i.QuerySelector(".check")!.TextContent));
        Assert.Equal([false, true, false], items.Select(i => i.ClassList.Contains("next")));
        Assert.Equal("1 / 3 done", page.Find(".progress").TextContent);
    }

    [Fact]
    public async Task Workout_left_open_in_the_routine_shows_in_progress()
    {
        await SeedAsync();
        SetUpSavedWorkout(new WorkoutState { TypeId = typeIds[1], RoutineItemId = itemIds[1], Started = true });

        var page = Open();

        Assert.Equal("In progress", page.FindAll(".routine-item")[1].QuerySelector(".target")!.TextContent);
    }

    [Fact]
    public async Task Pause_and_resume_save_the_routine()
    {
        var page = await OpenAsync();

        await Button(page, "Pause").ClickAsync();
        Assert.False(LastSaved<RoutineState>("routineState").Running);

        await Button(page, "Resume").ClickAsync();
        Assert.True(LastSaved<RoutineState>("routineState").Running);
    }

    [Fact]
    public async Task Finish_saves_the_run_even_with_workouts_left_and_opens_the_routine()
    {
        using var trace = new TestTrace();
        using var spans = new ActivityCollector(a => a.OperationName == "SaveRoutineRun" && a.TraceId == trace.Id);
        using var saved = new MetricCollector<long>(Services.GetRequiredService<IMeterFactory>(), Telemetry.Name, "workouttracker.routines.saved");
        var id = Guid.Empty;
        var page = await OpenAsync(s => { s.DoneItemIds.Add(itemIds[0]); id = s.Id; });

        await Button(page, "Finish routine").ClickAsync();

        var run = Assert.Single(await Database.RoutineRunsAsync());
        Assert.Equal(id, run.ClientId);
        Assert.Equal(routine.Id, run.RoutineId);
        Assert.Equal(UtcNow.AddMinutes(-5), run.StartedAtUtc);
        Assert.Equal(UtcNow, run.FinishedAtUtc);
        JSInterop.VerifyInvoke("routineState.clear");
        JSInterop.VerifyNotInvoke("workoutState.clear");
        Assert.EndsWith($"routine/{routine.Id}", Uri);
        Assert.Equal("Legs and feet", Assert.Single(spans.Stopped).GetTagItem("workouttracker.routine"));
        Assert.Equal("Legs and feet", Assert.Single(saved.GetMeasurementSnapshot()).Tags["workouttracker.routine"]);
    }

    [Fact]
    public async Task Finish_also_ends_a_workout_left_open_in_the_routine()
    {
        await SeedAsync();
        SetUpSavedWorkout(new WorkoutState { TypeId = typeIds[1], RoutineItemId = itemIds[1], Started = true });
        var page = Open();

        await Button(page, "Finish routine").ClickAsync();

        JSInterop.VerifyInvoke("workoutState.clear");
    }

    [Fact]
    public async Task Abandon_asks_first_and_a_tap_elsewhere_cancels()
    {
        var page = await OpenAsync();

        await page.Find("button.danger").ClickAsync();
        Assert.Equal("Are you sure?", page.Find("button.danger").TextContent.Trim());

        await page.Find(".dismiss-layer").ClickAsync();
        Assert.Equal("Abandon", page.Find("button.danger").TextContent.Trim());
        JSInterop.VerifyNotInvoke("routineState.clear");
    }

    [Fact]
    public async Task Abandon_tapped_twice_drops_the_run_and_keeps_finished_workouts()
    {
        var page = await OpenAsync(s => s.DoneItemIds.Add(itemIds[0]));
        await Database.AddWorkoutAsync(typeIds[0], UtcNow.AddMinutes(-4));

        await page.Find("button.danger").ClickAsync();
        await page.Find("button.danger").ClickAsync();

        JSInterop.VerifyInvoke("routineState.clear");
        Assert.Empty(await Database.RoutineRunsAsync());
        Assert.Equal(1, await Database.CountWorkoutsAsync());
        Assert.Equal("http://localhost/", Uri);
    }

    [Fact]
    public async Task Loading_the_page_records_a_span_with_the_routine_id()
    {
        using var trace = new TestTrace();
        using var spans = new ActivityCollector(a => a.OperationName == "Load RoutineRunPage" && a.TraceId == trace.Id);

        await OpenAsync();

        Assert.Equal(routine.Id, Assert.Single(spans.Stopped).GetTagItem("workouttracker.routine_id"));
    }
}
