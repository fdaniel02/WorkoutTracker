using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
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

    public void Dispose() => factory.Dispose();
}
