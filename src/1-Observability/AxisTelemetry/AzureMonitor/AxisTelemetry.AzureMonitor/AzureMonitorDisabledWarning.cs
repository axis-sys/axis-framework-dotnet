using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AxisTelemetry.AzureMonitor;

internal sealed class AzureMonitorDisabledWarning(ILogger<AzureMonitorDisabledWarning> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "Azure Monitor connection string not found (checked AzureMonitorAxisOptions.ConnectionString, " +
            "'{EnvKey}' and '{SectionKey}'). Axis telemetry degraded to NullAxisTelemetry — " +
            "no spans, metrics or logs will be exported to Application Insights.",
            DependencyInjection.ConnectionStringKey,
            DependencyInjection.ConnectionStringSectionKey);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
