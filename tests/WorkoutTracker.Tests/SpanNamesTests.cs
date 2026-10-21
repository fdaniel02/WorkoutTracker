using System.Diagnostics;

namespace WorkoutTracker.Tests;

public class SpanNamesTests
{
    static readonly ActivitySource Components = new("Microsoft.AspNetCore.Components");
    static readonly ActivitySource Circuits = new("Microsoft.AspNetCore.Components.Server.Circuits");
    static readonly ActivitySource Other = new("WorkoutTracker.Tests.SpanNames");

    static string Readable(ActivitySource source, string name, params (string Key, string Value)[] tags)
    {
        using var collector = new ActivityCollector(source.Name, _ => false);
        using var activity = source.StartActivity(name)!;
        foreach (var (key, value) in tags)
            activity.SetTag(key, value);
        return SpanNames.Readable(activity);
    }

    [Fact]
    public void Event_names_the_event_component_and_method()
    {
        var name = Readable(Components, "Event onclick -> WorkoutTracker.Components.Pages.WorkoutPage.FinishAsync",
            ("aspnetcore.components.type", "WorkoutTracker.Components.Pages.WorkoutPage"),
            ("code.function.name", "FinishAsync"),
            ("aspnetcore.components.attribute.name", "onclick"));

        Assert.Equal("Click WorkoutPage.FinishAsync", name);
    }

    [Fact]
    public void Event_without_an_attribute_name_is_called_Event()
    {
        var name = Readable(Components, "Event",
            ("aspnetcore.components.type", "WorkoutTracker.Components.Pages.Home"),
            ("code.function.name", "AddAsync"));

        Assert.Equal("Event Home.AddAsync", name);
    }

    [Fact]
    public void Route_names_the_route_and_component_without_constraints()
    {
        var name = Readable(Components, "Route /type/{Id:int} -> WorkoutTracker.Components.Pages.WorkoutTypePage",
            ("aspnetcore.components.type", "WorkoutTracker.Components.Pages.WorkoutTypePage"),
            ("aspnetcore.components.route", "/type/{Id:int}"));

        Assert.Equal("Route /type/{Id} -> WorkoutTypePage", name);
    }

    [Fact]
    public void Circuit_drops_the_id()
    {
        var name = Readable(Circuits, "Circuit rsVrOiOtgCt77uo5GpF1soy_uMGPmxN6_dYPNski0bI",
            ("aspnetcore.components.circuit.id", "rsVrOiOtgCt77uo5GpF1soy_uMGPmxN6_dYPNski0bI"));

        Assert.Equal("Circuit", name);
    }

    [Theory]
    [InlineData("GET /type/{Id:int}", "GET /type/{Id}")]
    [InlineData("GET /files/{*path}", "GET /files/{*path}")]
    [InlineData("GET /{page?}", "GET /{page}")]
    [InlineData("SaveWorkout", "SaveWorkout")]
    public void Other_spans_only_lose_route_constraints(string original, string expected) =>
        Assert.Equal(expected, Readable(Other, original));

    [Fact]
    public void Component_span_without_known_tags_keeps_its_name() =>
        Assert.Equal("Something", Readable(Components, "Something"));

    [Fact]
    public void Processor_renames_the_span_when_it_ends()
    {
        using var collector = new ActivityCollector(Circuits.Name, _ => false);
        using var activity = Circuits.StartActivity("Circuit abc")!;

        new SpanNames().OnEnd(activity);

        Assert.Equal("Circuit", activity.DisplayName);
    }
}
