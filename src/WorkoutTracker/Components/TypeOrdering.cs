using WorkoutTracker.Data;

namespace WorkoutTracker.Components;

public enum TypeOrder { Name, LastDone }

public static class TypeOrdering
{
    static readonly StringComparer ByName = StringComparer.CurrentCultureIgnoreCase;

    public static List<(WorkoutType Type, DateTime? LastUtc)> Sort(IEnumerable<(WorkoutType Type, DateTime? LastUtc)> types, TypeOrder order) =>
        order == TypeOrder.LastDone
            ? [.. types.OrderBy(t => t.LastUtc ?? DateTime.MinValue).ThenBy(t => t.Type.Name, ByName)]
            : [.. types.OrderBy(t => t.Type.Name, ByName)];

    public static TypeOrder Parse(string? saved) =>
        Enum.TryParse<TypeOrder>(saved, out var order) && Enum.IsDefined(order) ? order : TypeOrder.Name;
}
