using Bunit;
using Microsoft.EntityFrameworkCore;
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
    public async Task Adding_a_new_name_closes_the_dialog_and_lists_it()
    {
        var page = Render<Home>();
        page.WaitForElement(".list");

        await page.Find(".fab").ClickAsync();
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

        await page.Find(".fab").ClickAsync();
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
}
