using System.Text.Json;
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
        Services.AddScoped<RoutineStateStore>();
        Services.AddMetrics();
        Services.AddSingleton<WorkoutMetrics>();
        Services.AddSingleton<TimeProvider>(Time);
        Services.AddSingleton(new Clock(TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam"), Time));

        // Unplanned JS calls return default values: no wake lock, and no saved workout in localStorage.
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    protected static IElement Button<TPage>(IRenderedComponent<TPage> page, string text) where TPage : IComponent =>
        page.FindAll("button").Single(b => b.TextContent.Trim() == text);

    protected DateTime UtcNow => Time.GetUtcNow().UtcDateTime;

    protected string Uri => Services.GetRequiredService<NavigationManager>().Uri;

    protected void SetUpSavedWorkout(WorkoutState state) =>
        JSInterop.Setup<string?>("workoutState.load").SetResult(JsonSerializer.Serialize(state));

    protected void SetUpSavedRoutine(RoutineState state) =>
        JSInterop.Setup<string?>("routineState.load").SetResult(JsonSerializer.Serialize(state));

    protected T LastSaved<T>(string jsObject) =>
        JsonSerializer.Deserialize<T>(JSInterop.Invocations[$"{jsObject}.save"][^1].Arguments[0]!.ToString()!)!;

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Database.Dispose();
    }
}
