using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace WorkoutTracker.Tests.Data;

// Holds every async query until Release(), so a test can see what a page shows while it loads.
public sealed class BlockingQueryInterceptor : DbCommandInterceptor
{
    readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Release() => released.TrySetResult();

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        await released.Task.WaitAsync(cancellationToken);
        return result;
    }
}
