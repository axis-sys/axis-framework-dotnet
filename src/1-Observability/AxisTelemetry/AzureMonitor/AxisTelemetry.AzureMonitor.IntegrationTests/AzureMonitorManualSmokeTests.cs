using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace AxisTelemetry.AzureMonitor.IntegrationTests;

/// <summary>
/// Manual smoke test against a REAL Application Insights resource. It reads the connection string
/// from the project's user secrets (id: axis-telemetry-azure-monitor-tests) and publishes one span,
/// one metric, and one log stamped with a unique marker. Without the secret it self-skips — safe on
/// CI. Run it explicitly with:
/// <code>
/// dotnet user-secrets set "ConnectionStrings:ApplicationInsights" "InstrumentationKey=..." --project src/1-Observability/AxisTelemetry/AzureMonitor/AxisTelemetry.AzureMonitor.IntegrationTests
/// dotnet test --filter "Category=Manual" src/1-Observability/AxisTelemetry/AzureMonitor/AxisTelemetry.AzureMonitor.IntegrationTests
/// </code>
/// </summary>
[Trait("Category", "Manual")]
public class AzureMonitorManualSmokeTests
{
    [Fact]
    public async Task PublishTelemetry_ToRealApplicationInsights()
    {
        // Arrange — the real connection string comes from user secrets, through the same
        // ConnectionStrings:ApplicationInsights key the package resolves in production
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<AzureMonitorManualSmokeTests>(optional: true)
            .Build();
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(configuration.GetConnectionString("ApplicationInsights")),
            "No 'ConnectionStrings:ApplicationInsights' user secret set — manual smoke test skipped.");

        var services = new ServiceCollection();
        services.AddAzureMonitorAxis(configuration, o =>
        {
            o.ServiceName = "axis-smoke-test";
            o.ServiceVersion = typeof(DependencyInjection).Assembly.GetName().Version?.ToString();
        });
        await using var provider = services.BuildServiceProvider();

        // The distro attaches the trace/log exporters from a hosted service — do what the host does.
        foreach (var hostedService in provider.GetServices<IHostedService>())
            await hostedService.StartAsync(TestContext.Current.CancellationToken);

        var marker = $"axis-smoke-{Guid.NewGuid():N}";
        var tracerProvider = provider.GetRequiredService<TracerProvider>();

        // Act — one signal of each kind, all stamped with the marker
        var telemetry = provider.GetRequiredService<IAxisTelemetry>();
        using (var span = telemetry.StartSpan($"AxisSmoke.{marker}", AxisSpanKind.Client))
        {
            span.SetTag("axis.smoke.marker", marker);
            span.SetStatus(AxisSpanStatus.Ok);
        }

        provider.GetRequiredService<IAxisMetrics>().IncrementCounter(
            "axis.smoke.counter", 1, new KeyValuePair<string, object?>("axis.smoke.marker", marker));

        provider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Axis.SmokeTest")
            .LogWarning("Axis smoke marker {Marker}", marker);

        // Assert — every signal leaves the process (accepted by the real ingestion endpoint)
        Assert.True(tracerProvider.ForceFlush(30_000), "trace flush timed out");
        Assert.True(provider.GetRequiredService<MeterProvider>().ForceFlush(30_000), "metric flush timed out");
        provider.GetRequiredService<LoggerProvider>().ForceFlush(30_000);

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"Published to Application Insights. Search marker: {marker}");
    }
}
