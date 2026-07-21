using Axis;
using Axis.OpenTelemetry;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace AxisTelemetry.AzureMonitor;

public static class DependencyInjection
{
    internal const string ConnectionStringKey = "APPLICATIONINSIGHTS_CONNECTION_STRING";
    internal const string ConnectionStringName = "ApplicationInsights";
    internal const string ConnectionStringSectionKey = "ConnectionStrings:" + ConnectionStringName;

    /// <summary>
    /// Registers the Axis telemetry ports backed by the official Azure Monitor OpenTelemetry distro.
    /// Without a connection string (options, <c>APPLICATIONINSIGHTS_CONNECTION_STRING</c> or
    /// <c>ConnectionStrings:ApplicationInsights</c>) it degrades gracefully: registers
    /// <see cref="NullAxisTelemetry"/>, logs a warning at startup and exports nothing.
    /// </summary>
    public static IServiceCollection AddAzureMonitorAxis(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<AzureMonitorAxisOptions>? configure = null)
    {
        var options = new AzureMonitorAxisOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        var connectionString = ResolveConnectionString(options, configuration);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddSingleton<IAxisTelemetry>(NullAxisTelemetry.Instance);
            services.AddSingleton<IAxisMetrics>(NullAxisTelemetry.Instance);
            services.AddLogging();
            services.AddHostedService<AzureMonitorDisabledWarning>();
            return services;
        }

        services.AddOpenTelemetryAxis();

        var openTelemetry = services.AddOpenTelemetry()
            .UseAzureMonitor(monitor =>
            {
                monitor.ConnectionString = connectionString;
                // Since distro 1.5 the default sampler is rate-limited (5 traces/s) and it
                // overrides SamplingRatio; null restores deterministic ratio-based sampling.
                monitor.TracesPerSecond = options.TracesPerSecond;
                monitor.SamplingRatio = options.SamplingRatio;
                monitor.EnableLiveMetrics = options.EnableLiveMetrics;
            })
            .WithTracing(tracing => tracing.AddSource(OpenTelemetryAdapter.SourceName))
            .WithMetrics(metrics => metrics.AddMeter(OpenTelemetryAdapter.SourceName));

        if (!string.IsNullOrWhiteSpace(options.ServiceName))
            openTelemetry.ConfigureResource(resource =>
                resource.AddService(options.ServiceName, serviceVersion: options.ServiceVersion));

        if (options.ResourceAttributes.Count > 0)
            openTelemetry.ConfigureResource(resource => resource.AddAttributes(options.ResourceAttributes));

        ConfigureLogExport(services, options);
        return services;
    }

    private static string? ResolveConnectionString(AzureMonitorAxisOptions options, IConfiguration configuration)
        => options.ConnectionString
           ?? configuration[ConnectionStringKey]
           ?? configuration.GetConnectionString(ConnectionStringName);

    // Filters apply only to the OpenTelemetryLoggerProvider (the export pipeline): local providers
    // (console, debug) keep their own verbosity — only what is ingested (billed) is trimmed.
    private static void ConfigureLogExport(IServiceCollection services, AzureMonitorAxisOptions options)
    {
        services.Configure<OpenTelemetryLoggerOptions>(logger =>
        {
            logger.IncludeScopes = options.IncludeScopes;
            logger.IncludeFormattedMessage = options.IncludeFormattedMessage;
        });

        services.AddLogging(logging =>
        {
            if (!options.EnableLogExport)
            {
                logging.AddFilter<OpenTelemetryLoggerProvider>(null, LogLevel.None);
                return;
            }

            logging.AddFilter<OpenTelemetryLoggerProvider>(null, options.MinimumLogLevel);
            foreach (var (category, level) in options.CategoryLogLevels)
                logging.AddFilter<OpenTelemetryLoggerProvider>(category, level);
        });
    }
}
