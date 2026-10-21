using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using WorkoutTracker;
using WorkoutTracker.Components;
using WorkoutTracker.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContextFactory<AppDbContext>((services, o) =>
    o.UseAppDatabase(services.GetRequiredService<IConfiguration>(), retryOnFailure: true));
// Without EF's retries: StartupRetry already retries the migration, and the two loops would multiply.
builder.Services.AddKeyedSingleton<IDbContextFactory<AppDbContext>>(DatabaseMigrator.DbFactoryKey, (services, _) =>
    new PooledDbContextFactory<AppDbContext>(new DbContextOptionsBuilder<AppDbContext>()
        .UseAppDatabase(services.GetRequiredService<IConfiguration>(), retryOnFailure: false).Options));

builder.Services.AddAppTelemetry(builder.Configuration);
builder.Services.AddSingleton<WorkoutMetrics>();

builder.Services.AddScoped<WorkoutStateStore>();
builder.Services.AddSingleton<DatabaseMigrator>();
builder.Services.AddSingleton(TimeProvider.System);
// Resolved after Build(), so a bad setting is logged like any other startup error.
builder.Services.AddSingleton(services =>
{
    var timeZoneId = builder.Configuration["TimeZone"]
        ?? throw new InvalidOperationException("Setting 'TimeZone' is missing (an IANA id, e.g. 'Europe/Amsterdam').");
    return new Clock(TimeZoneInfo.FindSystemTimeZoneById(timeZoneId), services.GetRequiredService<TimeProvider>());
});

// Disposing the app on exit flushes the console logger's queue, so the last error is written before the process ends.
await using var app = builder.Build();

try
{
    app.Services.GetRequiredService<Clock>();
    await app.Services.GetRequiredService<DatabaseMigrator>().MigrateAsync();

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }
    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

    app.UseAntiforgery();

    app.MapStaticAssets();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    await app.RunAsync();
    return 0;
}
// dotnet ef and WebApplicationFactory stop the app with HostAbortedException; it isn't an error.
catch (Exception ex) when (ex is not HostAbortedException)
{
    // Logged here, not left to the runtime: an unhandled exception goes to stderr as multi-line text, not JSON.
    app.Logger.ApplicationFailed(ex);
    return 1;
}
