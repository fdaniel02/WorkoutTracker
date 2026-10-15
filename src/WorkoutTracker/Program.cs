using Microsoft.EntityFrameworkCore;
using WorkoutTracker;
using WorkoutTracker.Components;
using WorkoutTracker.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContextFactory<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException(
            "Connection string 'Default' is missing. Set it with " +
            "'dotnet user-secrets set \"ConnectionStrings:Default\" \"...\"' for local development, " +
            "or the environment variable 'ConnectionStrings__Default' in Docker.")));

builder.Services.AddScoped<WorkoutStateStore>();
builder.Services.AddSingleton<DatabaseMigrator>();
// Resolved after Build(), so a bad setting is logged like any other startup error.
builder.Services.AddSingleton(_ =>
{
    var timeZoneId = builder.Configuration["TimeZone"]
        ?? throw new InvalidOperationException("Setting 'TimeZone' is missing (an IANA id, e.g. 'Europe/Amsterdam').");
    return new Clock(TimeZoneInfo.FindSystemTimeZoneById(timeZoneId), TimeProvider.System);
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
