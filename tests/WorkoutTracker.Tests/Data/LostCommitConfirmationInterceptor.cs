using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace WorkoutTracker.Tests.Data;

// The first INSERT into Workouts completes (and commits), then fails as if the connection dropped before the
// confirmation arrived. SQL Server's retrying execution strategy treats a TimeoutException as transient.
public sealed class LostCommitConfirmationInterceptor : DbCommandInterceptor
{
    int failed;

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        DbDataReader result, CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("INSERT INTO [Workouts]") && Interlocked.Exchange(ref failed, 1) == 0)
        {
            // Closing the reader finishes the statement, so the row is committed before the "lost" confirmation.
            await result.DisposeAsync();
            throw new TimeoutException("Simulated: connection lost after the commit.");
        }
        return result;
    }

    public bool Failed => failed == 1;
}
