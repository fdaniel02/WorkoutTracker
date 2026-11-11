using WorkoutTracker.Components;

namespace WorkoutTracker.Tests;

public class TypeOrderingTests
{
    static readonly DateTime Oct1 = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
    static readonly DateTime Oct10 = new(2026, 10, 10, 8, 0, 0, DateTimeKind.Utc);

    static ListEntry Type(string name, DateTime? lastUtc) => new(0, name, lastUtc);

    static string[] Names(List<ListEntry> sorted) => [.. sorted.Select(t => t.Name)];

    [Fact]
    public void By_name_ignores_case()
    {
        var sorted = TypeOrdering.Sort([Type("squats", Oct1), Type("Push ups", Oct10), Type("burpees", null)], TypeOrder.Name);

        Assert.Equal(["burpees", "Push ups", "squats"], Names(sorted));
    }

    [Fact]
    public void By_last_done_puts_never_done_first_then_the_oldest()
    {
        var sorted = TypeOrdering.Sort([Type("Recent", Oct10), Type("Never", null), Type("Old", Oct1)], TypeOrder.LastDone);

        Assert.Equal(["Never", "Old", "Recent"], Names(sorted));
    }

    [Fact]
    public void By_last_done_sorts_ties_by_name()
    {
        var sorted = TypeOrdering.Sort(
            [Type("squats", Oct1), Type("Never b", null), Type("burpees", Oct1), Type("never a", null)],
            TypeOrder.LastDone);

        Assert.Equal(["never a", "Never b", "burpees", "squats"], Names(sorted));
    }

    [Theory]
    [InlineData("Name", TypeOrder.Name)]
    [InlineData("LastDone", TypeOrder.LastDone)]
    [InlineData(null, TypeOrder.Name)]
    [InlineData("", TypeOrder.Name)]
    [InlineData("Something else", TypeOrder.Name)]
    [InlineData("7", TypeOrder.Name)]
    public void Parse_falls_back_to_name_for_anything_unknown(string? saved, TypeOrder expected) =>
        Assert.Equal(expected, TypeOrdering.Parse(saved));

    static readonly List<ListEntry> FilterEntries =
    [
        new(1, "Starred in routine", null, Favorite: true, InRoutine: true),
        new(2, "Starred", null, Favorite: true),
        new(3, "In routine", null, InRoutine: true),
        new(4, "Plain", null),
    ];

    [Fact]
    public void Hiding_routine_only_drops_unstarred_types_that_are_in_a_routine_and_keeps_the_order() =>
        Assert.Equal(["Starred in routine", "Starred", "Plain"], Names(TypeOrdering.Filter(FilterEntries, TypeFilter.HideRoutineOnly)));

    [Fact]
    public void All_keeps_every_type() =>
        Assert.Equal(["Starred in routine", "Starred", "In routine", "Plain"], Names(TypeOrdering.Filter(FilterEntries, TypeFilter.All)));

    [Theory]
    [InlineData("HideRoutineOnly", TypeFilter.HideRoutineOnly)]
    [InlineData("All", TypeFilter.All)]
    [InlineData(null, TypeFilter.HideRoutineOnly)]
    [InlineData("", TypeFilter.HideRoutineOnly)]
    [InlineData("Something else", TypeFilter.HideRoutineOnly)]
    [InlineData("7", TypeFilter.HideRoutineOnly)]
    public void ParseFilter_falls_back_to_hiding_for_anything_unknown(string? saved, TypeFilter expected) =>
        Assert.Equal(expected, TypeOrdering.ParseFilter(saved));
}
