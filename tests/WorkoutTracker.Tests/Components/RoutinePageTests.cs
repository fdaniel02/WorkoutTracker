using Bunit;
using WorkoutTracker.Components;
using WorkoutTracker.Components.Pages;
using WorkoutTracker.Data;
using WorkoutTracker.Tests.Data;

namespace WorkoutTracker.Tests.Components;

// Today is Thu 15 Oct 2026 in Amsterdam (see PageTestContext); the grid starts on Mon 29 Jun 2026.
public class RoutinePageTests : PageTestContext
{
    static readonly DateOnly GridStart = new(2026, 6, 29);

    async Task<Routine> AddRoutineAsync()
    {
        var calves = await Database.AddWorkoutTypeAsync("Calf raise");
        var squat = await Database.AddWorkoutTypeAsync("Split squat");
        var plank = await Database.AddWorkoutTypeAsync("Side plank");
        return await Database.AddRoutineAsync("Legs and feet",
            (squat.Id, 3, "8 per side"), (calves.Id, 3, "8-12 per side"), (plank.Id, null, "30 s"));
    }

    IRenderedComponent<RoutinePage> RenderPage(int id)
    {
        var page = Render<RoutinePage>(p => p.Add(x => x.Id, id));
        page.WaitForElement(".heatmap");
        return page;
    }

    [Fact]
    public void Unknown_routine_shows_not_found()
    {
        var page = Render<RoutinePage>(p => p.Add(x => x.Id, 999));

        page.WaitForAssertion(() => Assert.Equal("Routine not found.", page.Find("p.muted").TextContent));
    }

    [Fact]
    public async Task Lists_the_workouts_in_order_with_their_targets()
    {
        var routine = await AddRoutineAsync();

        var page = RenderPage(routine.Id);

        Assert.Equal("Legs and feet", page.Find("h1").TextContent);
        var items = page.FindAll(".routine-items .list-item")
            .Select(i => (i.QuerySelector("span")!.TextContent, i.QuerySelector(".target")?.TextContent))
            .ToList();
        Assert.Equal([("Split squat", "3 × 8 per side"), ("Calf raise", "3 × 8-12 per side"), ("Side plank", "30 s")], items);
    }

    [Fact]
    public async Task Heatmap_marks_the_days_runs_started_and_the_total_counts_all_runs()
    {
        var routine = await AddRoutineAsync();
        await Database.AddRoutineRunAsync(routine.Id, new DateTime(2025, 1, 10, 8, 0, 0, DateTimeKind.Utc));
        await Database.AddRoutineRunAsync(routine.Id, new DateTime(2026, 10, 14, 8, 0, 0, DateTimeKind.Utc));

        var page = RenderPage(routine.Id);

        var done = page.FindAll(".cell")
            .Select((cell, i) => (cell, day: GridStart.AddDays(i)))
            .Where(x => x.cell.ClassList.Contains("done"))
            .Select(x => x.day);
        Assert.Equal([new DateOnly(2026, 10, 14)], done);
        Assert.Equal("Done 2 times", page.Find("div.muted").TextContent);
    }

    [Theory]
    [InlineData(0, "Not done yet")]
    [InlineData(1, "Done once")]
    public async Task Total_text_for_few_runs(int runs, string expected)
    {
        var routine = await AddRoutineAsync();
        for (var i = 0; i < runs; i++)
            await Database.AddRoutineRunAsync(routine.Id, new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc));

        Assert.Equal(expected, RenderPage(routine.Id).Find("div.muted").TextContent);
    }

    [Fact]
    public async Task Start_saves_a_running_routine_and_opens_it()
    {
        var routine = await AddRoutineAsync();
        var page = RenderPage(routine.Id);

        await Button(page, "Start routine").ClickAsync();

        var saved = LastSaved<RoutineState>("routineState");
        Assert.Equal(routine.Id, saved.RoutineId);
        Assert.Equal(UtcNow, saved.StartedUtc);
        Assert.True(saved.Running);
        Assert.EndsWith($"routine/{routine.Id}/run", Uri);
    }

    [Fact]
    public async Task Start_is_disabled_without_workouts()
    {
        var routine = await Database.AddRoutineAsync("Empty");

        var page = RenderPage(routine.Id);

        Assert.True(Button(page, "Start routine").HasAttribute("disabled"));
        Assert.Equal("This routine has no workouts yet.", page.Find("main > p.muted").TextContent);
    }

    [Fact]
    public async Task This_routine_in_progress_can_be_continued()
    {
        var routine = await AddRoutineAsync();
        SetUpSavedRoutine(RoutineState.Start(routine.Id, UtcNow));

        var page = RenderPage(routine.Id);

        Assert.Equal($"routine/{routine.Id}/run", page.Find("a.btn").GetAttribute("href"));
        Assert.Empty(page.FindAll("button.btn"));
    }

    [Fact]
    public async Task Another_routine_in_progress_blocks_starting()
    {
        var routine = await AddRoutineAsync();
        var other = await Database.AddRoutineAsync("Fingers");
        SetUpSavedRoutine(RoutineState.Start(other.Id, UtcNow));

        var page = RenderPage(routine.Id);

        Assert.Equal("Finish or abandon Fingers first.", page.Find(".blocked").TextContent);
        Assert.Equal($"routine/{other.Id}/run", page.Find(".blocked a").GetAttribute("href"));
        Assert.Empty(page.FindAll("button.btn"));
    }

    [Fact]
    public async Task A_workout_in_progress_blocks_starting()
    {
        var routine = await AddRoutineAsync();
        var pushUps = await Database.AddWorkoutTypeAsync("Push-ups");
        SetUpSavedWorkout(new WorkoutState { TypeId = pushUps.Id, StartedUtc = UtcNow, Started = true });

        var page = RenderPage(routine.Id);

        Assert.Equal("Finish or abandon Push-ups first.", page.Find(".blocked").TextContent);
        Assert.Equal($"workout/{pushUps.Id}", page.Find(".blocked a").GetAttribute("href"));
    }

    [Fact]
    public async Task Loading_the_page_records_a_span_with_the_routine_id()
    {
        var routine = await AddRoutineAsync();
        using var trace = new TestTrace();
        using var spans = new ActivityCollector(a => a.OperationName == "Load RoutinePage" && a.TraceId == trace.Id);

        RenderPage(routine.Id);

        var span = Assert.Single(spans.Stopped);
        Assert.Equal(routine.Id, span.GetTagItem("workouttracker.routine_id"));
    }
}
