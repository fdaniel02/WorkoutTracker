using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace WorkoutTracker.Tests;

public class TelemetryTests
{
    static ServiceProvider Services(params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build();
        return new ServiceCollection().AddLogging().AddAppTelemetry(configuration).BuildServiceProvider();
    }

    [Fact]
    public void Nothing_is_registered_without_a_collector_endpoint()
    {
        using var services = Services();

        Assert.Null(services.GetService<TracerProvider>());
        Assert.Null(services.GetService<MeterProvider>());
    }

    [Fact]
    public void Traces_and_metrics_are_set_up_with_a_collector_endpoint()
    {
        using var services = Services(("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317"));

        var tracing = services.GetRequiredService<TracerProvider>();
        Assert.NotNull(services.GetService<MeterProvider>());
        Assert.Equal(Telemetry.Name, ServiceName(tracing));
    }

    [Fact]
    public void Service_name_comes_from_OTEL_SERVICE_NAME()
    {
        using var services = Services(("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317"), ("OTEL_SERVICE_NAME", "workouts-test"));

        Assert.Equal("workouts-test", ServiceName(services.GetRequiredService<TracerProvider>()));
    }

    static object? ServiceName(TracerProvider tracing) =>
        tracing.GetResource().Attributes.Single(a => a.Key == "service.name").Value;
}
