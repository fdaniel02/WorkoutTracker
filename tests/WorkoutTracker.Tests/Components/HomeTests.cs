using Bunit;
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

    async Task AddTypeLastDoneAsync(string name, DateTime lastUtc)
    {
        var type = await Database.AddWorkoutTypeAsync(name);
        await Database.AddWorkoutAsync(type.Id, lastUtc.AddDays(-5));
        await Database.AddWorkoutAsync(type.Id, lastUtc);
    }
}
