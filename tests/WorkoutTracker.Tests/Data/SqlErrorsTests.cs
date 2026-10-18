using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WorkoutTracker.Data;

namespace WorkoutTracker.Tests.Data;

// SqlException has no public constructor, so the errors come from a real SQL Server.
[Collection("SqlServer")]
public class SqlErrorsTests(SqlServerFixture sqlServer)
{
    static async Task<SqlException> OpenFails(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        return await Assert.ThrowsAsync<SqlException>(() => connection.OpenAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Wrong_password_is_permanent()
    {
        var error = await OpenFails(sqlServer.ConnectionString("master", password: "wrong-Password1"));

        Assert.Equal(18456, error.Number);
        Assert.True(SqlErrors.IsPermanent(error));
    }

    [Fact]
    public async Task Missing_database_is_permanent()
    {
        var error = await OpenFails(sqlServer.ConnectionString(SqlServerFixture.NewDatabaseName()));

        Assert.Equal(4060, error.Number);
        Assert.True(SqlErrors.IsPermanent(error));
    }

    [Fact]
    public async Task Unreachable_server_is_retried()
    {
        var error = await OpenFails("Server=127.0.0.1,1;Connect Timeout=2;User Id=sa;Password=x;TrustServerCertificate=true");

        Assert.False(SqlErrors.IsPermanent(error));
    }

    [Fact]
    public void Errors_other_than_SqlException_are_permanent()
    {
        Assert.True(SqlErrors.IsPermanent(new ArgumentException("Format of the initialization string is not valid")));
    }

    [Fact]
    public async Task Unique_constraint_violation_is_a_unique_violation()
    {
        await using var connection = new SqlConnection(sqlServer.ConnectionString("master"));
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new SqlCommand("CREATE TABLE #t (Name nvarchar(10) UNIQUE); INSERT #t VALUES ('a'), ('a');", connection);

        var error = await Assert.ThrowsAsync<SqlException>(() => command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

        Assert.Equal(2627, error.Number);
        Assert.True(SqlErrors.IsUniqueViolation(new DbUpdateException("Save failed", error)));
    }

    [Fact]
    public async Task Other_sql_errors_are_not_a_unique_violation()
    {
        var error = await OpenFails(sqlServer.ConnectionString(SqlServerFixture.NewDatabaseName()));

        Assert.False(SqlErrors.IsUniqueViolation(new DbUpdateException("Save failed", error)));
    }

    [Fact]
    public void Errors_other_than_SqlException_are_not_a_unique_violation()
    {
        Assert.False(SqlErrors.IsUniqueViolation(new DbUpdateException("Save failed", new InvalidOperationException())));
    }
}
