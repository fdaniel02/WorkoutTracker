using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using WorkoutTracker.Data;
using WorkoutTracker.Tests.Data;

namespace WorkoutTracker.Tests;

// The whole app with its production configuration, against a real SQL Server.
[Collection("SqlServer")]
public sealed class StartupTests : IDisposable
{
    readonly WebApplicationFactory<Program> factory;

    public StartupTests(SqlServerFixture sqlServer)
    {
        var connectionString = sqlServer.ConnectionString(SqlServerFixture.NewDatabaseName());
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseEnvironment("Production")
            .UseSetting("ConnectionStrings:Default", connectionString));
    }

    [Fact]
    public async Task App_starts_and_serves_the_home_page()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        // The page renders without prerendering, so only the app shell that starts Blazor is in the response.
        Assert.Contains("_framework/blazor.web", html);
    }

    [Fact]
    public void Logs_are_json_and_skip_ef_core_sql_commands()
    {
        var console = factory.Services.GetRequiredService<IOptionsMonitor<ConsoleLoggerOptions>>().CurrentValue;
        var loggers = factory.Services.GetRequiredService<ILoggerFactory>();

        Assert.Equal(ConsoleFormatterNames.Json, console.FormatterName);
        Assert.False(loggers.CreateLogger("Microsoft.EntityFrameworkCore.Database.Command").IsEnabled(LogLevel.Information));
        Assert.True(loggers.CreateLogger("Microsoft.EntityFrameworkCore.Migrations").IsEnabled(LogLevel.Information));
    }

    [Fact]
    public async Task Pages_retry_database_outages_but_the_migration_does_not()
    {
        var pages = factory.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var migration = factory.Services.GetRequiredKeyedService<IDbContextFactory<AppDbContext>>(DatabaseMigrator.DbFactoryKey);

        await using var pageDb = await pages.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await using var migrationDb = await migration.CreateDbContextAsync(TestContext.Current.CancellationToken);
        Assert.True(pageDb.Database.CreateExecutionStrategy().RetriesOnFailure);
        Assert.False(migrationDb.Database.CreateExecutionStrategy().RetriesOnFailure);
    }

    public void Dispose() => factory.Dispose();
}
