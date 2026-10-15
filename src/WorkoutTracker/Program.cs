using Microsoft.EntityFrameworkCore;
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

var timeZoneId = builder.Configuration["TimeZone"]
    ?? throw new InvalidOperationException("Setting 'TimeZone' is missing (an IANA id, e.g. 'Europe/Amsterdam').");
builder.Services.AddScoped<WorkoutStateStore>();
builder.Services.AddSingleton(new Clock(TimeZoneInfo.FindSystemTimeZoneById(timeZoneId), TimeProvider.System));

var app = builder.Build();

await MigrateWithRetryAsync(app.Services);

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

app.Run();

static async Task MigrateWithRetryAsync(IServiceProvider services)
{
    // The SQL Server container may still be starting when the app boots.
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            await using var db = await services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            await db.Database.MigrateAsync();
            return;
        }
        catch when (attempt < 15)
        {
            await Task.Delay(TimeSpan.FromSeconds(4));
        }
    }
}
