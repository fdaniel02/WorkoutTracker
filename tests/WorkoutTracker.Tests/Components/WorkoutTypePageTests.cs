using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using WorkoutTracker.Components.Pages;
using WorkoutTracker.Tests.Data;

namespace WorkoutTracker.Tests.Components;

// Today is Thu 15 Oct 2026 in Amsterdam (see PageTestContext); the grid starts on Mon 29 Jun 2026.
public class WorkoutTypePageTests : PageTestContext
{
    static readonly DateOnly GridStart = new(2026, 6, 29);

    IRenderedComponent<WorkoutTypePage> RenderPage(int typeId)
    {
        var page = Render<WorkoutTypePage>(p => p.Add(x => x.Id, typeId));
        page.WaitForElement(".heatmap");
        return page;
    }

    static IElement Cell(IRenderedComponent<WorkoutTypePage> page, DateOnly day) =>
        page.FindAll(".cell")[day.DayNumber - GridStart.DayNumber];

    [Fact]
    public void Unknown_type_shows_not_found_and_no_title()
    {
        var head = Render<HeadOutlet>();

        var page = Render<WorkoutTypePage>(p => p.Add(x => x.Id, 999));

        page.WaitForAssertion(() => Assert.Equal("Workout type not found.", page.Find("p.muted").TextContent));
        Assert.Equal("", head.Find("title").TextContent);
    }

    [Fact]
    public async Task Page_title_is_the_type_name()
    {
        var type = await Database.AddWorkoutTypeAsync("Push-ups");
        var head = Render<HeadOutlet>();

        RenderPage(type.Id);

        head.WaitForAssertion(() => Assert.Equal("Push-ups", head.Find("title").TextContent));
    }

    [Fact]
    public async Task Loading_the_page_records_a_span_with_the_type_id()
    {
        var type = await Database.AddWorkoutTypeAsync("Push-ups");
        using var trace = new TestTrace();
        using var spans = new ActivityCollector(a => a.OperationName == "Load WorkoutTypePage" && a.TraceId == trace.Id);

        RenderPage(type.Id);

        var span = Assert.Single(spans.Stopped);
        Assert.Equal(type.Id, span.GetTagItem("workouttracker.workout_type_id"));
    }

    [Fact]
    public async Task Total_counts_workouts_older_than_the_grid()
    {
        var type = await Database.AddWorkoutTypeAsync("Push-ups");
        await Database.AddWorkoutAsync(type.Id, new DateTime(2025, 1, 10, 8, 0, 0));
        await Database.AddWorkoutAsync(type.Id, new DateTime(2026, 10, 14, 8, 0, 0));

        var page = RenderPage(type.Id);

        Assert.Equal("2 workouts total", page.Find("div.muted").TextContent);
    }

    [Fact]
    public async Task Workouts_are_marked_on_their_local_date_from_the_first_grid_day()
    {
        var type = await Database.AddWorkoutTypeAsync("Push-ups");
        // 00:30 on Mon 29 Jun in Amsterdam (UTC+2): the first grid day.
        await Database.AddWorkoutAsync(type.Id, new DateTime(2026, 6, 28, 22, 30, 0));
        // 23:30 on Sun 28 Jun in Amsterdam: before the grid.
        await Database.AddWorkoutAsync(type.Id, new DateTime(2026, 6, 28, 21, 30, 0));
        await Database.AddWorkoutAsync(type.Id, new DateTime(2026, 10, 14, 8, 0, 0));

        var page = RenderPage(type.Id);

        var done = page.FindAll(".cell")
            .Select((cell, i) => (cell, day: GridStart.AddDays(i)))
            .Where(x => x.cell.ClassList.Contains("done"))
            .Select(x => x.day);
        Assert.Equal([GridStart, new DateOnly(2026, 10, 14)], done);
        Assert.Equal("3 workouts total", page.Find("div.muted").TextContent);
    }

    [Fact]
    public async Task Today_is_outlined_and_later_days_are_hidden()
    {
        var type = await Database.AddWorkoutTypeAsync("Push-ups");
        await Database.AddWorkoutAsync(type.Id, new DateTime(2026, 10, 1, 8, 0, 0));

        var page = RenderPage(type.Id);

        Assert.Equal(16 * 7, page.FindAll(".cell").Count);
        Assert.Single(page.FindAll(".cell.today"));
        Assert.True(Cell(page, new DateOnly(2026, 10, 15)).ClassList.Contains("today"));
        Assert.Equal(3, page.FindAll(".cell.future").Count);
        Assert.True(Cell(page, new DateOnly(2026, 10, 16)).ClassList.Contains("future"));
        Assert.Equal("1 workout total", page.Find("div.muted").TextContent);
    }

    [Fact]
    public async Task Month_labels_sit_above_their_week_columns()
    {
        var type = await Database.AddWorkoutTypeAsync("Push-ups");

        var page = RenderPage(type.Id);

        var labels = page.FindAll(".month").Select(m => m.TextContent).ToList();
        Assert.Equal(16, labels.Count);
        Assert.Equal(["Jul", "", "", "", "Aug", "", "", "", "", "Sep", "", "", "", "Oct", "", ""], labels);
        // Each label is followed by its week's 7 cells, so it lands in the same grid column.
        var children = page.Find(".heatmap").Children;
        Assert.All(Enumerable.Range(0, 16), w => Assert.True(children[w * 8].ClassList.Contains("month")));
    }
}

public class WorkoutTypePageLoadingTests : PageTestContext
{
    readonly BlockingQueryInterceptor queryGate;

    public WorkoutTypePageLoadingTests() : this(new BlockingQueryInterceptor()) { }

    WorkoutTypePageLoadingTests(BlockingQueryInterceptor queryGate) : base(queryGate) => this.queryGate = queryGate;

    [Fact]
    public async Task Shows_loading_until_the_type_is_loaded()
    {
        var type = await Database.AddWorkoutTypeAsync("Push-ups");

        var page = Render<WorkoutTypePage>(p => p.Add(x => x.Id, type.Id));

        Assert.Equal("Loading…", page.Find("p.muted").TextContent);
        queryGate.Release();
        page.WaitForElement(".heatmap");
    }
}
