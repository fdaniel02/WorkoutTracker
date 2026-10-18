using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using WorkoutTracker.Components;
using WorkoutTracker.Data;
using WorkoutTracker.Tests.Data;

namespace WorkoutTracker.Tests.Components;

// Real pages, database and services; only the browser (JS interop, navigation) is faked.
public abstract class PageTestContext : BunitContext
{
    protected TestDatabase Database { get; }
    protected FixedTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));

    protected PageTestContext(params IInterceptor[] interceptors)
    {
        Database = new TestDatabase(interceptors);
        Services.AddSingleton<IDbContextFactory<AppDbContext>>(Database);
        Services.AddScoped<WorkoutStateStore>();
        Services.AddMetrics();
        Services.AddSingleton<WorkoutMetrics>();
        Services.AddSingleton(new Clock(TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam"), Time));

        // Unplanned JS calls return default values: no wake lock, and no saved workout in localStorage.
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    protected static IElement Button<TPage>(IRenderedComponent<TPage> page, string text) where TPage : IComponent =>
        page.FindAll("button").Single(b => b.TextContent.Trim() == text);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Database.Dispose();
    }
}
