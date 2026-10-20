using WorkoutTracker.Components;
using WorkoutTracker.Data;

namespace WorkoutTracker.Tests;

public class TypeOrderingTests
{
    static readonly DateTime Oct1 = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
    static readonly DateTime Oct10 = new(2026, 10, 10, 8, 0, 0, DateTimeKind.Utc);

    static (WorkoutType, DateTime?) Type(string name, DateTime? lastUtc) => (new WorkoutType { Name = name }, lastUtc);

    static string[] Names(List<(WorkoutType Type, DateTime? LastUtc)> sorted) => [.. sorted.Select(t => t.Type.Name)];

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
}
