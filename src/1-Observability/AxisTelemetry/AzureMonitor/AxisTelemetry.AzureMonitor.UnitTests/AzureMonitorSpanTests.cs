using Axis.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace AxisTelemetry.AzureMonitor.UnitTests;

[CollectionDefinition("AzureMonitorSpanCollection", DisableParallelization = true)]
public class AzureMonitorSpanCollection;

[Collection("AzureMonitorSpanCollection")]
public class AzureMonitorSpanTests : IDisposable
{
    private const string TestConnectionString =
        "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://test.invalid/;LiveEndpoint=https://test.invalid/";

    private readonly ActivityListener _listener;
    private readonly List<Activity> _activities = [];

    public AzureMonitorSpanTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == OpenTelemetryAdapter.SourceName,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => _activities.Add(activity)
        };
        ActivitySource.AddActivityListener(_listener);
    }

    [Fact]
    public void StartSpan_WithAzureMonitorRegistered_ShouldEmitFromAxisActivitySource()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddAzureMonitorAxis(new ConfigurationBuilder().Build(), o => o.ConnectionString = TestConnectionString);
        using var serviceProvider = services.BuildServiceProvider();
        var telemetry = serviceProvider.GetRequiredService<IAxisTelemetry>();

        // Act
        using var span = telemetry.StartSpan("AzureMonitor.TestOperation");

        // Assert
        Assert.Single(_activities);
        Assert.Equal("AzureMonitor.TestOperation", _activities[0].OperationName);
        Assert.Equal(OpenTelemetryAdapter.SourceName, _activities[0].Source.Name);
        Assert.False(string.IsNullOrWhiteSpace(span.TraceId));
    }

    [Fact]
    public void StartSpan_WithoutConnectionString_ShouldBeSilentNoOp()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddAzureMonitorAxis(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var telemetry = serviceProvider.GetRequiredService<IAxisTelemetry>();

        // Act
        using var span = telemetry.StartSpan("AzureMonitor.NoOp");

        // Assert
        Assert.Empty(_activities);
        Assert.Same(NullAxisSpan.Instance, span);
    }

    public void Dispose() => _listener.Dispose();
}
