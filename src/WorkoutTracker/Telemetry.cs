using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace WorkoutTracker;

public static class Telemetry
{
    public const string Name = "WorkoutTracker";

    public static readonly ActivitySource ActivitySource = new(Name);

    static readonly string[] BlazorSources = ["Microsoft.AspNetCore.Components", "Microsoft.AspNetCore.Components.Server.Circuits"];
    static readonly string[] BlazorMeters =
        ["Microsoft.AspNetCore.Components", "Microsoft.AspNetCore.Components.Lifecycle", "Microsoft.AspNetCore.Components.Server.Circuits"];

    // Only with a collector to send to: without one, the OTLP exporter keeps failing against localhost:4317.
    public static IServiceCollection AddAppTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            return services;

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(configuration["OTEL_SERVICE_NAME"] ?? Name))
            .WithTracing(tracing => tracing
                .AddSource(Name)
                .AddSource(BlazorSources)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSqlClientInstrumentation())
            .WithMetrics(metrics => metrics
                .AddMeter(Name)
                .AddMeter(BlazorMeters)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddSqlClientInstrumentation())
            .WithLogging()
            .UseOtlpExporter();
        return services;
    }
}
