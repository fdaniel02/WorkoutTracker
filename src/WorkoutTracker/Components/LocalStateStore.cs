using System.Text.Json;
using Microsoft.JSInterop;

namespace WorkoutTracker.Components;

public abstract class LocalStateStore<T>(IJSRuntime js, string jsObject) where T : class
{
    public async Task<T?> LoadAsync()
    {
        var json = await js.InvokeAsync<string?>($"{jsObject}.load");
        if (json is null) return null;
        try { return JsonSerializer.Deserialize<T>(json); }
        catch (JsonException) { return null; }
    }

    public Task SaveAsync(T state) =>
        js.InvokeVoidAsync($"{jsObject}.save", JsonSerializer.Serialize(state)).AsTask();

    public Task ClearAsync() => js.InvokeVoidAsync($"{jsObject}.clear").AsTask();
}
