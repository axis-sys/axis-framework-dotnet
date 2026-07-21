using Microsoft.Extensions.Logging;

namespace AxisTelemetry.AzureMonitor.UnitTests;

public class AzureMonitorAxisOptionsTests
{
    [Fact]
    public void Defaults_ShouldExportEverythingWithLiveMetrics()
    {
        // Arrange & Act
        var options = new AzureMonitorAxisOptions();

        // Assert
        Assert.Null(options.ConnectionString);
        Assert.Equal(1.0f, options.SamplingRatio);
        Assert.Null(options.TracesPerSecond);
        Assert.True(options.EnableLiveMetrics);
        Assert.Null(options.ServiceName);
        Assert.Null(options.ServiceVersion);
        Assert.Empty(options.ResourceAttributes);
    }

    [Fact]
    public void Defaults_LogExport_ShouldBeInformationWithoutScopes()
    {
        // Arrange & Act
        var options = new AzureMonitorAxisOptions();

        // Assert
        Assert.True(options.EnableLogExport);
        Assert.Equal(LogLevel.Information, options.MinimumLogLevel);
        Assert.Empty(options.CategoryLogLevels);
        Assert.False(options.IncludeScopes);
        Assert.False(options.IncludeFormattedMessage);
    }
}
