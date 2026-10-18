using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
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
