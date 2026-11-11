namespace WorkoutTracker.Components;

public enum TypeOrder { Name, LastDone }

// A workout type or a routine, as listed on Home.
public record ListEntry(int Id, string Name, DateTime? LastUtc);

public static class TypeOrdering
{
    static readonly StringComparer ByName = StringComparer.CurrentCultureIgnoreCase;

    public static List<ListEntry> Sort(IEnumerable<ListEntry> entries, TypeOrder order) =>
        order == TypeOrder.LastDone
            ? [.. entries.OrderBy(e => e.LastUtc ?? DateTime.MinValue).ThenBy(e => e.Name, ByName)]
            : [.. entries.OrderBy(e => e.Name, ByName)];

    public static TypeOrder Parse(string? saved) =>
        Enum.TryParse<TypeOrder>(saved, out var order) && Enum.IsDefined(order) ? order : TypeOrder.Name;
}
