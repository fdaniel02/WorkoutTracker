using Bunit;
using Microsoft.EntityFrameworkCore;
using WorkoutTracker.Components;
using WorkoutTracker.Components.Pages;

namespace WorkoutTracker.Tests.Components;

// Today is Thu 15 Oct 2026 in Amsterdam (see PageTestContext).
public class HomeTests : PageTestContext
{
    [Fact]
    public async Task Each_type_shows_how_long_ago_it_was_last_done()
    {
        await AddTypeLastDoneAsync("A today", new DateTime(2026, 10, 15, 8, 0, 0, DateTimeKind.Utc));
        await AddTypeLastDoneAsync("B yesterday", new DateTime(2026, 10, 14, 8, 0, 0, DateTimeKind.Utc));
        await AddTypeLastDoneAsync("C three days", new DateTime(2026, 10, 12, 8, 0, 0, DateTimeKind.Utc));
        await Database.AddWorkoutTypeAsync("D never");

        var page = Render<Home>();
        page.WaitForElement(".list");

        var ago = page.FindAll(".list .list-item")
            .Select(item => item.QuerySelector(".ago")?.TextContent)
            .ToList();
        Assert.Equal(["today", "1 day ago", "3 days ago", null], ago);
    }

    [Fact]
    public void Loading_the_page_records_a_span()
    {
        using var trace = new TestTrace();
        using var spans = new ActivityCollector(a => a.OperationName == "Load Home" && a.TraceId == trace.Id);

        Render<Home>().WaitForElement(".list");

        Assert.Single(spans.Stopped);
    }

    [Fact]
    public async Task Add_button_in_the_header_opens_an_empty_dialog()
    {
        var page = Render<Home>();
        page.WaitForElement(".list");
        var add = page.Find(".page-header .add");
        Assert.Equal("Add workout type", add.GetAttribute("aria-label"));
        Assert.Empty(page.FindAll(".dialog"));

        await add.ClickAsync();

        Assert.Equal("", page.Find(".dialog input").GetAttribute("value") ?? "");
    }

    [Fact]
    public async Task Adding_a_new_name_closes_the_dialog_and_lists_it()
    {
        var page = Render<Home>();
        page.WaitForElement(".list");

        await page.Find(".page-header .add").ClickAsync();
        await page.Find(".dialog input").InputAsync("  Push ups ");
        await page.Find(".dialog").SubmitAsync();

        page.WaitForAssertion(() => Assert.Empty(page.FindAll(".dialog")));
        Assert.Equal(["Push ups"], page.FindAll(".list .list-item span:first-child").Select(s => s.TextContent));
    }

    [Fact]
    public async Task Adding_an_existing_name_shows_a_message_and_keeps_the_dialog_open()
    {
        await Database.AddWorkoutTypeAsync("Push ups");
        var page = Render<Home>();
        page.WaitForElement(".list");

        await page.Find(".page-header .add").ClickAsync();
        await page.Find(".dialog input").InputAsync("Push ups");
        await page.Find(".dialog").SubmitAsync();

        page.WaitForAssertion(() => Assert.Equal("That workout type already exists.", page.Find(".dialog .error").TextContent));
        await using var db = Database.CreateDbContext();
        Assert.Equal(1, await db.WorkoutTypes.CountAsync(Xunit.TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Types_are_sorted_by_name_by_default()
    {
        await AddTypesForSortingAsync();

        var page = Render<Home>();
        page.WaitForElement(".list");

        Assert.Equal(["Burpees", "Push ups", "Squats"], TypeNames(page));
        Assert.Equal("Sorted by name. Tap to sort by last workout.", page.Find(".sort").GetAttribute("aria-label"));
    }

    [Fact]
    public async Task Sort_button_switches_to_last_workout_and_back_and_saves_each_choice()
    {
        await AddTypesForSortingAsync();
        var page = Render<Home>();
        page.WaitForElement(".list");

        await page.Find(".sort").ClickAsync();

        Assert.Equal(["Squats", "Burpees", "Push ups"], TypeNames(page));
        Assert.Equal("Sorted by last workout, oldest first. Tap to sort by name.", page.Find(".sort").GetAttribute("title"));
        Assert.Equal("LastDone", JSInterop.VerifyInvoke("typeOrder.save").Arguments[0]);

        await page.Find(".sort").ClickAsync();

        Assert.Equal(["Burpees", "Push ups", "Squats"], TypeNames(page));
        Assert.Equal("Name", JSInterop.VerifyInvoke("typeOrder.save", calledTimes: 2)[1].Arguments[0]);
    }

    [Fact]
    public async Task Saved_order_is_used_when_the_page_opens()
    {
        await AddTypesForSortingAsync();
        JSInterop.Setup<string?>("typeOrder.load").SetResult("LastDone");

        var page = Render<Home>();
        page.WaitForElement(".list");

        Assert.Equal(["Squats", "Burpees", "Push ups"], TypeNames(page));
    }

    async Task AddTypesForSortingAsync()
    {
        await AddTypeLastDoneAsync("Push ups", new DateTime(2026, 10, 14, 8, 0, 0, DateTimeKind.Utc));
        await AddTypeLastDoneAsync("Burpees", new DateTime(2026, 10, 2, 8, 0, 0, DateTimeKind.Utc));
        await Database.AddWorkoutTypeAsync("Squats");
    }

    static string[] TypeNames(IRenderedComponent<Home> page) =>
        [.. page.FindAll(".list .list-item span:first-child").Select(s => s.TextContent)];

    async Task AddTypeLastDoneAsync(string name, DateTime lastUtc)
    {
        var type = await Database.AddWorkoutTypeAsync(name);
        await Database.AddWorkoutAsync(type.Id, lastUtc.AddDays(-5));
        await Database.AddWorkoutAsync(type.Id, lastUtc);
    }

    async Task AddTypesForFilteringAsync()
    {
        var starredInRoutine = await Database.AddWorkoutTypeAsync("Burpees", favorite: true);
        var inRoutine = await Database.AddWorkoutTypeAsync("Lunges");
        await Database.AddWorkoutTypeAsync("Push ups");
        await Database.AddRoutineAsync("Legs", (starredInRoutine.Id, 3, null), (inRoutine.Id, 3, null));
    }

    [Fact]
    public async Task Unstarred_types_in_a_routine_are_hidden_by_default()
    {
        await AddTypesForFilteringAsync();

        var page = Render<Home>();
        page.WaitForElement(".list");

        Assert.Equal(["Burpees", "Push ups"], TypeNames(page));
        var filter = page.Find(".filter");
        Assert.Equal("true", filter.GetAttribute("aria-pressed"));
        Assert.Equal("Hiding unstarred workouts that are in a routine. Tap to show all.", filter.GetAttribute("aria-label"));
    }

    [Fact]
    public async Task Starred_types_are_marked()
    {
        await AddTypesForFilteringAsync();

        var page = Render<Home>();
        page.WaitForElement(".list");

        Assert.Equal(["Burpees"], page.FindAll(".list .favorite").Select(s => s.TextContent));
    }

    [Fact]
    public async Task Filter_button_shows_all_and_hides_again_and_saves_each_choice()
    {
        await AddTypesForFilteringAsync();
        var page = Render<Home>();
        page.WaitForElement(".list");

        await page.Find(".filter").ClickAsync();

        Assert.Equal(["Burpees", "Lunges", "Push ups"], TypeNames(page));
        Assert.Equal("Showing all workouts. Tap to hide unstarred workouts that are in a routine.", page.Find(".filter").GetAttribute("title"));
        Assert.Equal("All", JSInterop.VerifyInvoke("typeFilter.save").Arguments[0]);

        await page.Find(".filter").ClickAsync();

        Assert.Equal(["Burpees", "Push ups"], TypeNames(page));
        Assert.Equal("HideRoutineOnly", JSInterop.VerifyInvoke("typeFilter.save", calledTimes: 2)[1].Arguments[0]);
    }

    [Fact]
    public async Task Saved_filter_is_used_when_the_page_opens()
    {
        await AddTypesForFilteringAsync();
        JSInterop.Setup<string?>("typeFilter.load").SetResult("All");

        var page = Render<Home>();
        page.WaitForElement(".list");

        Assert.Equal(["Burpees", "Lunges", "Push ups"], TypeNames(page));
        Assert.Equal("false", page.Find(".filter").GetAttribute("aria-pressed"));
    }

    [Fact]
    public async Task All_types_hidden_says_how_to_show_them()
    {
        var type = await Database.AddWorkoutTypeAsync("Lunges");
        await Database.AddRoutineAsync("Legs", (type.Id, 3, null));

        var page = Render<Home>();
        page.WaitForElement(".list");

        Assert.Empty(TypeNames(page));
        Assert.Equal("All workouts are hidden. Tap the filter to show them.", page.Find("main > p.muted").TextContent);
    }

    [Fact]
    public void No_types_says_how_to_add_one()
    {
        var page = Render<Home>();
        page.WaitForElement(".list");

        Assert.Equal("No workout types yet. Tap + to add your first one.", page.Find("main > p.muted").TextContent);
    }

    [Fact]
    public async Task Filter_button_is_only_on_the_workouts_tab()
    {
        var page = Render<Home>();
        page.WaitForElement(".list");
        Assert.Single(page.FindAll(".page-header .filter"));

        await Button(page, "Routines").ClickAsync();

        Assert.Empty(page.FindAll(".page-header .filter"));
    }

    [Fact]
    public async Task Routines_tab_lists_routines_with_when_they_were_last_done_and_is_remembered()
    {
        var type = await Database.AddWorkoutTypeAsync("Push ups");
        var upper = await Database.AddRoutineAsync("Upper body", (type.Id, 3, null));
        await Database.AddRoutineRunAsync(upper.Id, new DateTime(2026, 10, 14, 8, 0, 0, DateTimeKind.Utc));
        await Database.AddRoutineAsync("Legs", (type.Id, 3, null));
        var page = Render<Home>();
        page.WaitForElement(".list");

        await Button(page, "Routines").ClickAsync();

        Assert.Equal("Routines", page.Find("h1").TextContent);
        Assert.Equal(["Legs", "Upper body"], TypeNames(page));
        Assert.Equal([null, "1 day ago"], page.FindAll(".list .list-item").Select(i => i.QuerySelector(".ago")?.TextContent));
        Assert.Equal($"routine/{upper.Id}", page.FindAll(".list .list-item")[1].GetAttribute("href"));
        Assert.Empty(page.FindAll(".page-header .add"));
        Assert.Equal("Routines", JSInterop.VerifyInvoke("homeView.save").Arguments[0]);
    }

    [Fact]
    public async Task Saved_view_is_used_when_the_page_opens_and_sorting_applies_to_it()
    {
        var type = await Database.AddWorkoutTypeAsync("Push ups");
        var upper = await Database.AddRoutineAsync("Upper body", (type.Id, 3, null));
        await Database.AddRoutineRunAsync(upper.Id, new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc));
        var legs = await Database.AddRoutineAsync("Legs", (type.Id, 3, null));
        await Database.AddRoutineRunAsync(legs.Id, new DateTime(2026, 10, 14, 8, 0, 0, DateTimeKind.Utc));
        JSInterop.Setup<string?>("homeView.load").SetResult("Routines");
        var page = Render<Home>();
        page.WaitForElement(".list");
        Assert.Equal(["Legs", "Upper body"], TypeNames(page));

        await page.Find(".sort").ClickAsync();

        Assert.Equal(["Upper body", "Legs"], TypeNames(page));
    }

    [Fact]
    public void No_routines_says_so()
    {
        JSInterop.Setup<string?>("homeView.load").SetResult("Routines");

        var page = Render<Home>();
        page.WaitForElement(".list");

        Assert.Equal("No routines yet.", page.Find("main > p.muted").TextContent);
    }

    [Fact]
    public async Task Routine_in_progress_is_continued_from_both_tabs_instead_of_its_open_workout()
    {
        var type = await Database.AddWorkoutTypeAsync("Push ups");
        var routine = await Database.AddRoutineAsync("Upper body", (type.Id, 3, null));
        var state = RoutineState.Start(routine.Id, UtcNow);
        state.DoneItemIds.Add(routine.Items[0].Id);
        SetUpSavedRoutine(state);
        SetUpSavedWorkout(new WorkoutState { TypeId = type.Id, RoutineItemId = routine.Items[0].Id, Started = true });
        var page = Render<Home>();
        page.WaitForElement(".list");

        var continueLink = page.Find(".continue");
        Assert.Equal("Continue Upper body", continueLink.QuerySelector("span")!.TextContent);
        Assert.Equal("1 done", continueLink.QuerySelector(".ago")!.TextContent);
        Assert.Equal($"routine/{routine.Id}/run", continueLink.GetAttribute("href"));

        await Button(page, "Routines").ClickAsync();

        Assert.Single(page.FindAll(".continue"));
    }
}
