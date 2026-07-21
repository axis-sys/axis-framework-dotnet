using Axis.OpenTelemetry;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Logs;

namespace AxisTelemetry.AzureMonitor.UnitTests;

public class DependencyInjectionTests
{
    private const string TestConnectionString =
        "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://test.invalid/;LiveEndpoint=https://test.invalid/";

    private static IConfiguration EmptyConfiguration() => new ConfigurationBuilder().Build();

    private static IConfiguration ConfigurationWith(string key, string value) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { [key] = value })
        .Build();

    [Fact]
    public void AddAzureMonitorAxis_WithoutConnectionString_ShouldRegisterNullAxisTelemetry()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAzureMonitorAxis(EmptyConfiguration());
        using var serviceProvider = services.BuildServiceProvider();

        // Assert
        var telemetry = serviceProvider.GetService<IAxisTelemetry>();
        var metrics = serviceProvider.GetService<IAxisMetrics>();
        Assert.Same(NullAxisTelemetry.Instance, telemetry);
        Assert.Same(NullAxisTelemetry.Instance, metrics);
    }

    [Fact]
    public void AddAzureMonitorAxis_WithoutConnectionString_ShouldNotThrow()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var exception = Record.Exception(() =>
        {
            services.AddAzureMonitorAxis(EmptyConfiguration());
            using var serviceProvider = services.BuildServiceProvider();
            serviceProvider.GetRequiredService<IAxisTelemetry>();
        });

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void AddAzureMonitorAxis_WithoutConnectionString_ShouldRegisterStartupWarning()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAzureMonitorAxis(EmptyConfiguration());
        using var serviceProvider = services.BuildServiceProvider();

        // Assert
        var hostedServices = serviceProvider.GetServices<IHostedService>();
        Assert.Contains(hostedServices, s => s is AzureMonitorDisabledWarning);
    }

    [Fact]
    public void AddAzureMonitorAxis_WithEnvironmentKeyConnectionString_ShouldRegisterOpenTelemetryAdapter()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = ConfigurationWith("APPLICATIONINSIGHTS_CONNECTION_STRING", TestConnectionString);

        // Act
        services.AddAzureMonitorAxis(configuration);
        using var serviceProvider = services.BuildServiceProvider();

        // Assert
        var telemetry = serviceProvider.GetService<IAxisTelemetry>();
        Assert.IsType<OpenTelemetryAdapter>(telemetry);
    }

    [Fact]
    public void AddAzureMonitorAxis_WithConnectionStringsSection_ShouldRegisterOpenTelemetryAdapter()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = ConfigurationWith("ConnectionStrings:ApplicationInsights", TestConnectionString);

        // Act
        services.AddAzureMonitorAxis(configuration);
        using var serviceProvider = services.BuildServiceProvider();

        // Assert
        var telemetry = serviceProvider.GetService<IAxisTelemetry>();
        Assert.IsType<OpenTelemetryAdapter>(telemetry);
    }

    [Fact]
    public void AddAzureMonitorAxis_WithOptionsConnectionString_ShouldRegisterOpenTelemetryAdapter()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAzureMonitorAxis(EmptyConfiguration(), o => o.ConnectionString = TestConnectionString);
        using var serviceProvider = services.BuildServiceProvider();

        // Assert
        var telemetry = serviceProvider.GetService<IAxisTelemetry>();
        Assert.IsType<OpenTelemetryAdapter>(telemetry);
    }

    [Fact]
    public void AddAzureMonitorAxis_WithConnectionString_ShouldBindBothPortsToSameAdapter()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAzureMonitorAxis(EmptyConfiguration(), o => o.ConnectionString = TestConnectionString);
        using var serviceProvider = services.BuildServiceProvider();

        // Assert
        var telemetry = serviceProvider.GetService<IAxisTelemetry>();
        var metrics = serviceProvider.GetService<IAxisMetrics>();
        var adapter = serviceProvider.GetService<OpenTelemetryAdapter>();
        Assert.Same(adapter, telemetry);
        Assert.Same(adapter, metrics);
    }

    [Fact]
    public void AddAzureMonitorAxis_ShouldApplySamplingRatio()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAzureMonitorAxis(EmptyConfiguration(), o =>
        {
            o.ConnectionString = TestConnectionString;
            o.SamplingRatio = 0.25f;
        });
        using var serviceProvider = services.BuildServiceProvider();

        // Assert — TracesPerSecond must be null so the ratio wins over the distro's rate limiter
        var monitorOptions = serviceProvider.GetRequiredService<IOptions<AzureMonitorOptions>>().Value;
        Assert.Equal(0.25f, monitorOptions.SamplingRatio);
        Assert.Null(monitorOptions.TracesPerSecond);
        Assert.Equal(TestConnectionString, monitorOptions.ConnectionString);
    }

    [Fact]
    public void AddAzureMonitorAxis_ShouldApplyTracesPerSecondRateLimit()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAzureMonitorAxis(EmptyConfiguration(), o =>
        {
            o.ConnectionString = TestConnectionString;
            o.TracesPerSecond = 2.5;
        });
        using var serviceProvider = services.BuildServiceProvider();

        // Assert
        var monitorOptions = serviceProvider.GetRequiredService<IOptions<AzureMonitorOptions>>().Value;
        Assert.Equal(2.5, monitorOptions.TracesPerSecond);
    }

    [Fact]
    public void AddAzureMonitorAxis_ShouldApplyLiveMetricsToggle()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAzureMonitorAxis(EmptyConfiguration(), o =>
        {
            o.ConnectionString = TestConnectionString;
            o.EnableLiveMetrics = false;
        });
        using var serviceProvider = services.BuildServiceProvider();

        // Assert
        var monitorOptions = serviceProvider.GetRequiredService<IOptions<AzureMonitorOptions>>().Value;
        Assert.False(monitorOptions.EnableLiveMetrics);
    }

    [Fact]
    public void AddAzureMonitorAxis_ShouldApplyMinimumLogLevelToExportPipelineOnly()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAzureMonitorAxis(EmptyConfiguration(), o =>
        {
            o.ConnectionString = TestConnectionString;
            o.MinimumLogLevel = LogLevel.Warning;
        });
        using var serviceProvider = services.BuildServiceProvider();

        // Assert
        var filterOptions = serviceProvider.GetRequiredService<IOptions<LoggerFilterOptions>>().Value;
        var rule = Assert.Single(filterOptions.Rules, r =>
            r.ProviderName == typeof(OpenTelemetryLoggerProvider).FullName && r.CategoryName is null);
        Assert.Equal(LogLevel.Warning, rule.LogLevel);
    }

    [Fact]
    public void AddAzureMonitorAxis_WithLogExportDisabled_ShouldFilterEverythingFromExportPipeline()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAzureMonitorAxis(EmptyConfiguration(), o =>
        {
            o.ConnectionString = TestConnectionString;
            o.EnableLogExport = false;
        });
        using var serviceProvider = services.BuildServiceProvider();

        // Assert
        var filterOptions = serviceProvider.GetRequiredService<IOptions<LoggerFilterOptions>>().Value;
        var rule = Assert.Single(filterOptions.Rules, r =>
            r.ProviderName == typeof(OpenTelemetryLoggerProvider).FullName);
        Assert.Equal(LogLevel.None, rule.LogLevel);
    }

    [Fact]
    public void AddAzureMonitorAxis_ShouldApplyCategoryLogLevelOverrides()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAzureMonitorAxis(EmptyConfiguration(), o =>
        {
            o.ConnectionString = TestConnectionString;
            o.CategoryLogLevels["Microsoft.AspNetCore"] = LogLevel.Warning;
        });
        using var serviceProvider = services.BuildServiceProvider();

        // Assert
        var filterOptions = serviceProvider.GetRequiredService<IOptions<LoggerFilterOptions>>().Value;
        var rule = Assert.Single(filterOptions.Rules, r =>
            r.ProviderName == typeof(OpenTelemetryLoggerProvider).FullName && r.CategoryName == "Microsoft.AspNetCore");
        Assert.Equal(LogLevel.Warning, rule.LogLevel);
    }

    [Fact]
    public void AddAzureMonitorAxis_ShouldApplyLogVerbosityToggles()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAzureMonitorAxis(EmptyConfiguration(), o =>
        {
            o.ConnectionString = TestConnectionString;
            o.IncludeScopes = true;
            o.IncludeFormattedMessage = true;
        });
        using var serviceProvider = services.BuildServiceProvider();

        // Assert
        var loggerOptions = serviceProvider.GetRequiredService<IOptions<OpenTelemetryLoggerOptions>>().Value;
        Assert.True(loggerOptions.IncludeScopes);
        Assert.True(loggerOptions.IncludeFormattedMessage);
    }

    [Fact]
    public void AddAzureMonitorAxis_ShouldRegisterConfiguredOptionsAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        AzureMonitorAxisOptions? captured = null;

        // Act
        services.AddAzureMonitorAxis(EmptyConfiguration(), o =>
        {
            o.ConnectionString = TestConnectionString;
            captured = o;
        });
        using var serviceProvider = services.BuildServiceProvider();

        // Assert
        var resolved = serviceProvider.GetService<AzureMonitorAxisOptions>();
        Assert.NotNull(resolved);
        Assert.Same(captured, resolved);
    }

    [Fact]
    public void AddAzureMonitorAxis_ShouldReturnBuilderForChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddAzureMonitorAxis(EmptyConfiguration());

        // Assert
        Assert.Same(services, result);
    }
}
