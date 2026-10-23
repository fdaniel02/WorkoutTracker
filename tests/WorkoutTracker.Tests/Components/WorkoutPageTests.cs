using System.Diagnostics.Metrics;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.JSInterop;
using WorkoutTracker.Components;
using WorkoutTracker.Components.Pages;
using WorkoutTracker.Tests.Data;

namespace WorkoutTracker.Tests.Components;

public class WorkoutPageTests : PageTestContext
{
    readonly BlockingSaveInterceptor saveGate;

    public WorkoutPageTests() : this(new BlockingSaveInterceptor()) { }

    WorkoutPageTests(BlockingSaveInterceptor saveGate) : base(saveGate) => this.saveGate = saveGate;

    async Task<(IRenderedComponent<WorkoutPage> Page, int TypeId)> StartWorkoutAsync()
    {
        var type = await Database.AddWorkoutTypeAsync("Push-ups");
        var page = Render<WorkoutPage>(p => p.Add(x => x.Id, type.Id));
        page.WaitForElement("button.wide");
        await Button(page, "Start").ClickAsync();
        return (page, type.Id);
    }

    [Fact(Timeout = 10_000)]
    public async Task Finish_tapped_again_while_saving_is_ignored()
    {
        var (page, typeId) = await StartWorkoutAsync();
        // Captured before the re-render: on a phone the second tap can arrive before the disabled button does.
        var finish = Button(page, "Finish");

        var firstTap = finish.ClickAsync();
        await saveGate.SaveStarted.WaitAsync(Xunit.TestContext.Current.CancellationToken);
        // Not awaited before Release(): if the tap isn't ignored, it waits on the blocked save too.
        var secondTap = finish.ClickAsync();
        saveGate.Release();
        await Task.WhenAll(firstTap, secondTap);

        Assert.Equal(1, await Database.CountWorkoutsAsync());
        Assert.EndsWith($"type/{typeId}", Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public async Task Loading_the_page_records_a_span_with_the_type_id()
    {
        var type = await Database.AddWorkoutTypeAsync("Push-ups");
        using var trace = new TestTrace();
        using var spans = new ActivityCollector(a => a.OperationName == "Load WorkoutPage" && a.TraceId == trace.Id);

        Render<WorkoutPage>(p => p.Add(x => x.Id, type.Id)).WaitForElement("button.wide");

        var span = Assert.Single(spans.Stopped);
        Assert.Equal(type.Id, span.GetTagItem("workouttracker.workout_type_id"));
    }

    [Fact]
    public async Task Finish_records_a_span_and_counts_the_workout()
    {
        using var trace = new TestTrace();
        using var spans = new ActivityCollector(a => a.OperationName == "SaveWorkout" && a.TraceId == trace.Id);
        using var saved = new MetricCollector<long>(Services.GetRequiredService<IMeterFactory>(), Telemetry.Name, "workouttracker.workouts.saved");
        var (page, _) = await StartWorkoutAsync();
        await Button(page, "+1").ClickAsync();
        saveGate.Release();

        await Button(page, "Finish").ClickAsync();

        var span = Assert.Single(spans.Stopped);
        Assert.Equal("Push-ups", span.GetTagItem("workouttracker.workout_type"));
        Assert.Equal(1, span.GetTagItem("workouttracker.reps"));
        var measurement = Assert.Single(saved.GetMeasurementSnapshot());
        Assert.Equal(1, measurement.Value);
        Assert.Equal("Push-ups", measurement.Tags["workouttracker.workout_type"]);
    }

    [Fact]
    public async Task Timer_button_shows_start_then_pause_then_resume()
    {
        var type = await Database.AddWorkoutTypeAsync("Push-ups");
        var page = Render<WorkoutPage>(p => p.Add(x => x.Id, type.Id));
        page.WaitForElement("button.wide");

        await Button(page, "Start").ClickAsync();
        await Button(page, "Pause").ClickAsync();

        Assert.NotNull(Button(page, "Resume"));
    }

    [Fact]
    public async Task Leaving_the_page_after_the_browser_disconnected_does_not_throw()
    {
        JSInterop.SetupVoid("wakeLock.off").SetException(new JSDisconnectedException("Circuit gone"));
        await StartWorkoutAsync();

        var exception = await Record.ExceptionAsync(DisposeComponentsAsync);

        Assert.Null(exception);
        JSInterop.VerifyInvoke("wakeLock.off");
    }

    [Fact]
    public async Task Timer_shows_the_time_since_start()
    {
        var (page, _) = await StartWorkoutAsync();

        Time.UtcNow += TimeSpan.FromSeconds(75);

        page.WaitForAssertion(() => Assert.Equal("01:15", Timer(page)), TickTimeout);
    }

    [Fact]
    public async Task Rep_starts_a_rest_and_Start_times_the_next_set_from_zero()
    {
        var (page, _) = await StartWorkoutAsync();
        Time.UtcNow += TimeSpan.FromSeconds(40);

        await Button(page, "+1").ClickAsync();

        Assert.Equal("Rest", page.Find(".rest-label").TextContent);
        Assert.Equal(["1"], RepCircles(page));
        Assert.Null(CurrentRep(page));
        Assert.Equal("00:00", Timer(page));
        Time.UtcNow += TimeSpan.FromSeconds(20);
        page.WaitForAssertion(() => Assert.Equal("00:20", Timer(page)), TickTimeout);

        await Button(page, "Start").ClickAsync();

        Assert.Empty(page.FindAll(".rest-label"));
        Assert.Equal("2", CurrentRep(page));
        Assert.Equal("00:00", Timer(page));
        Assert.NotNull(Button(page, "+1"));
    }

    [Fact]
    public async Task Each_rep_adds_a_numbered_circle()
    {
        var (page, _) = await StartWorkoutAsync();
        Assert.Empty(RepCircles(page));
        Assert.Equal("1", CurrentRep(page));

        for (var i = 0; i < 3; i++)
        {
            await Button(page, "+1").ClickAsync();
            await Button(page, "Start").ClickAsync();
        }

        Assert.Equal(["1", "2", "3"], RepCircles(page));
        Assert.Equal("4", CurrentRep(page));
        Assert.Equal("3 repetitions", page.Find(".rep-circles").GetAttribute("aria-label"));
    }

    [Fact]
    public async Task Abandon_asks_first_and_a_tap_elsewhere_cancels()
    {
        var (page, _) = await StartWorkoutAsync();

        await page.Find("button.danger").ClickAsync();
        Assert.Equal("Are you sure?", page.Find("button.danger").TextContent.Trim());

        await page.Find(".dismiss-layer").ClickAsync();
        Assert.Equal("Abandon", page.Find("button.danger").TextContent.Trim());
        Assert.Empty(page.FindAll(".dismiss-layer"));
        JSInterop.VerifyNotInvoke("workoutState.clear");
    }

    [Fact]
    public async Task Taps_on_the_page_send_no_event_unless_Abandon_is_confirming()
    {
        var (page, _) = await StartWorkoutAsync();

        Assert.Empty(page.FindAll(".dismiss-layer"));
        // bUnit bubbles a click up to the ancestors' handlers, so none on <main> means +1, Start and Finish send only their own.
        await Assert.ThrowsAsync<MissingEventHandlerException>(() => page.Find("main").ClickAsync());
    }

    [Fact]
    public async Task Abandon_tapped_twice_clears_the_workout_and_goes_home()
    {
        var (page, _) = await StartWorkoutAsync();

        await page.Find("button.danger").ClickAsync();
        await page.Find("button.danger").ClickAsync();

        JSInterop.VerifyInvoke("workoutState.clear");
        Assert.Equal("http://localhost/", Services.GetRequiredService<NavigationManager>().Uri);
        Assert.Equal(0, await Database.CountWorkoutsAsync());
    }

    [Fact]
    public async Task Saved_workout_of_this_type_is_restored()
    {
        var type = await Database.AddWorkoutTypeAsync("Push-ups");
        SetUpSavedWorkout(new WorkoutState
        {
            TypeId = type.Id,
            StartedUtc = Time.GetUtcNow().UtcDateTime.AddMinutes(-10),
            Reps = 3,
            Started = true,
            RunningSinceUtc = Time.GetUtcNow().UtcDateTime.AddSeconds(-90)
        });

        var page = Render<WorkoutPage>(p => p.Add(x => x.Id, type.Id));

        page.WaitForAssertion(() => Assert.Equal(["1", "2", "3"], RepCircles(page)));
        Assert.Equal("4", CurrentRep(page));
        Assert.Equal("01:30", Timer(page));
        Assert.NotNull(Button(page, "Pause"));
    }

    [Fact]
    public async Task Finish_keeps_a_saved_workout_of_another_type()
    {
        var (page, _) = await OpenWithAnotherTypeSavedAsync();
        saveGate.Release();

        await Button(page, "Finish").ClickAsync();

        Assert.Equal(1, await Database.CountWorkoutsAsync());
        JSInterop.VerifyNotInvoke("workoutState.clear");
    }

    [Fact]
    public async Task Abandon_keeps_a_saved_workout_of_another_type()
    {
        var (page, _) = await OpenWithAnotherTypeSavedAsync();

        await page.Find("button.danger").ClickAsync();
        await page.Find("button.danger").ClickAsync();

        Assert.Equal("http://localhost/", Services.GetRequiredService<NavigationManager>().Uri);
        JSInterop.VerifyNotInvoke("workoutState.clear");
    }

    [Fact]
    public void Unknown_type_goes_home()
    {
        var navigation = Services.GetRequiredService<NavigationManager>();

        var page = Render<WorkoutPage>(p => p.Add(x => x.Id, 999));

        page.WaitForAssertion(() => Assert.Equal("http://localhost/", navigation.Uri));
    }

    [Fact]
    public async Task Finish_saves_when_the_page_was_opened_and_when_it_was_finished()
    {
        var openedAt = Time.GetUtcNow().UtcDateTime;
        var (page, typeId) = await StartWorkoutAsync();
        Time.UtcNow += TimeSpan.FromMinutes(12);
        saveGate.Release();

        await Button(page, "Finish").ClickAsync();

        await using var db = Database.CreateDbContext();
        var workout = await db.Workouts.SingleAsync(Xunit.TestContext.Current.CancellationToken);
        Assert.Equal(typeId, workout.WorkoutTypeId);
        Assert.Equal(openedAt, workout.StartedAtUtc);
        Assert.Equal(openedAt.AddMinutes(12), workout.FinishedAtUtc);
    }

    [Fact]
    public async Task Finish_saves_the_workout_with_its_id()
    {
        var (page, _) = await StartWorkoutAsync();
        var saved = JsonSerializer.Deserialize<WorkoutState>(
            JSInterop.Invocations["workoutState.save"][^1].Arguments[0]!.ToString()!)!;
        saveGate.Release();

        await Button(page, "Finish").ClickAsync();

        await using var db = Database.CreateDbContext();
        var workout = await db.Workouts.SingleAsync(Xunit.TestContext.Current.CancellationToken);
        Assert.NotEqual(Guid.Empty, saved.Id);
        Assert.Equal(saved.Id, workout.ClientId);
    }

    [Fact]
    public async Task Restored_workout_keeps_its_id()
    {
        var type = await Database.AddWorkoutTypeAsync("Push-ups");
        var id = Guid.NewGuid();
        SetUpSavedWorkout(new WorkoutState { Id = id, TypeId = type.Id, StartedUtc = Time.GetUtcNow().UtcDateTime, Started = true });
        var page = Render<WorkoutPage>(p => p.Add(x => x.Id, type.Id));
        page.WaitForElement("button.wide");
        saveGate.Release();

        await Button(page, "Finish").ClickAsync();

        await using var db = Database.CreateDbContext();
        Assert.Equal(id, (await db.Workouts.SingleAsync(Xunit.TestContext.Current.CancellationToken)).ClientId);
    }

    // The page redraws the timer on a real 250 ms tick, so a moved clock shows up within a few ticks.
    static readonly TimeSpan TickTimeout = TimeSpan.FromSeconds(5);

    static string Timer(IRenderedComponent<WorkoutPage> page) => page.Find(".timer").TextContent;

    static IEnumerable<string> RepCircles(IRenderedComponent<WorkoutPage> page) =>
        page.FindAll(".rep-circles .rep-circle:not(.current)").Select(c => c.TextContent);

    static string? CurrentRep(IRenderedComponent<WorkoutPage> page) =>
        page.FindAll(".rep-circles .rep-circle.current").SingleOrDefault()?.TextContent;

    void SetUpSavedWorkout(WorkoutState state) =>
        JSInterop.Setup<string?>("workoutState.load").SetResult(JsonSerializer.Serialize(state));

    async Task<(IRenderedComponent<WorkoutPage> Page, int TypeId)> OpenWithAnotherTypeSavedAsync()
    {
        var other = await Database.AddWorkoutTypeAsync("Squats");
        var type = await Database.AddWorkoutTypeAsync("Push-ups");
        SetUpSavedWorkout(new WorkoutState { TypeId = other.Id, StartedUtc = Time.GetUtcNow().UtcDateTime, Reps = 5, Started = true });
        var page = Render<WorkoutPage>(p => p.Add(x => x.Id, type.Id));
        page.WaitForElement("button.wide");
        Assert.Empty(page.Find(".rep-circles").Children);
        return (page, type.Id);
    }

    [Fact(Timeout = 10_000)]
    public async Task Finish_is_disabled_while_saving()
    {
        var (page, _) = await StartWorkoutAsync();

        var tap = Button(page, "Finish").ClickAsync();
        await saveGate.SaveStarted.WaitAsync(Xunit.TestContext.Current.CancellationToken);

        page.WaitForAssertion(() => Assert.True(Button(page, "Finish").HasAttribute("disabled")));
        saveGate.Release();
        await tap;
    }
}
