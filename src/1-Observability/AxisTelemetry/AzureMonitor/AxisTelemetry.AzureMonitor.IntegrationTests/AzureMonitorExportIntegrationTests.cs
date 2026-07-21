using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace AxisTelemetry.AzureMonitor.IntegrationTests;

[Collection("FakeIngestionCollection")]
public class AzureMonitorExportIntegrationTests(FakeIngestionFixture fixture)
{
    private static readonly TimeSpan IngestionTimeout = TimeSpan.FromSeconds(10);

    // The distro attaches the trace/log exporters from a hosted service, so the tests start the
    // IHostedServices exactly as the production host would.
    private async Task<ServiceProvider> BuildStartedProviderAsync(Action<AzureMonitorAxisOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddAzureMonitorAxis(new ConfigurationBuilder().Build(), o =>
        {
            o.ConnectionString = fixture.ConnectionString;
            o.EnableLiveMetrics = false;
            configure?.Invoke(o);
        });

        var provider = services.BuildServiceProvider();
        foreach (var hostedService in provider.GetServices<IHostedService>())
            await hostedService.StartAsync(TestContext.Current.CancellationToken);
        return provider;
    }

    [Fact]
    public async Task StartSpan_ShouldExportToIngestionEndpoint()
    {
        // Arrange
        await using var provider = await BuildStartedProviderAsync();
        var tracerProvider = provider.GetRequiredService<TracerProvider>(); // boots the SDK pipeline
        var telemetry = provider.GetRequiredService<IAxisTelemetry>();
        var operationName = $"integration.span.{Guid.NewGuid():N}";

        // Act
        using (var span = telemetry.StartSpan(operationName, AxisSpanKind.Client))
            span.SetTag("integration.test", true);
        tracerProvider.ForceFlush();

        // Assert
        Assert.True(
            await fixture.WaitForPayloadAsync(operationName, IngestionTimeout),
            $"requests={fixture.RequestCount}; payloads:\n{fixture.DumpPayloads(400)}");
    }

    [Fact]
    public async Task IncrementCounter_ShouldExportToIngestionEndpoint()
    {
        // Arrange
        await using var provider = await BuildStartedProviderAsync();
        _ = provider.GetRequiredService<TracerProvider>();
        var meterProvider = provider.GetRequiredService<MeterProvider>();
        var metrics = provider.GetRequiredService<IAxisMetrics>();
        var counterName = $"integration.counter.{Guid.NewGuid():N}";

        // Act
        metrics.IncrementCounter(counterName, 3);
        meterProvider.ForceFlush();

        // Assert
        Assert.True(await fixture.WaitForPayloadAsync(counterName, IngestionTimeout));
    }

    [Fact]
    public async Task Logger_ShouldExportWarning_AndFilterInformation_WhenMinimumIsWarning()
    {
        // Arrange
        await using var provider = await BuildStartedProviderAsync(o => o.MinimumLogLevel = LogLevel.Warning);
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("Axis.IntegrationTests");
        var informationMarker = $"info-{Guid.NewGuid():N}";
        var warningMarker = $"warn-{Guid.NewGuid():N}";

        // Act
        logger.LogInformation("integration marker {Marker}", informationMarker);
        logger.LogWarning("integration marker {Marker}", warningMarker);
        provider.GetRequiredService<LoggerProvider>().ForceFlush();

        // Assert — information was logged first: if the warning arrived and it did not, it was filtered
        Assert.True(await fixture.WaitForPayloadAsync(warningMarker, IngestionTimeout));
        Assert.False(fixture.ContainsPayload(informationMarker));
    }

    [Fact]
    public async Task Logger_ShouldExportNothing_WhenLogExportDisabled()
    {
        // Arrange
        await using var provider = await BuildStartedProviderAsync(o => o.EnableLogExport = false);
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("Axis.IntegrationTests");
        var criticalMarker = $"crit-{Guid.NewGuid():N}";

        // Act
        logger.LogCritical("integration marker {Marker}", criticalMarker);
        provider.GetRequiredService<LoggerProvider>().ForceFlush();

        // Assert
        Assert.False(await fixture.WaitForPayloadAsync(criticalMarker, TimeSpan.FromSeconds(2)));
    }
}
