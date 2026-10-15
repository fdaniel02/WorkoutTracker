using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace WorkoutTracker.Tests.Data;

// Holds every SaveChangesAsync until Release(), so a test can act while a save is in progress.
public sealed class BlockingSaveInterceptor : SaveChangesInterceptor
{
    readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task SaveStarted => started.Task;

    public void Release() => released.TrySetResult();

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        started.TrySetResult();
        await released.Task.WaitAsync(cancellationToken);
        return result;
    }
}
