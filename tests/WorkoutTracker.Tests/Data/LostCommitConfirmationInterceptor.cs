using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace WorkoutTracker.Tests.Data;

// The first INSERT into the table completes (and commits), then fails as if the connection dropped before the
// confirmation arrived. Npgsql's retrying execution strategy treats a TimeoutException as transient.
public sealed class LostCommitConfirmationInterceptor(string table = "Workouts") : DbCommandInterceptor
{
    int failed;

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        DbDataReader result, CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains($"INSERT INTO \"{table}\"") && Interlocked.Exchange(ref failed, 1) == 0)
        {
            // Closing the reader finishes the statement, so the row is committed before the "lost" confirmation.
            await result.DisposeAsync();
            throw new TimeoutException("Simulated: connection lost after the commit.");
        }
        return result;
    }

    public bool Failed => failed == 1;
}
