using Microsoft.Extensions.Logging;

namespace AxisTelemetry.AzureMonitor;

public sealed class AzureMonitorAxisOptions
{
    /// <summary>
    /// Overrides the connection string resolved from configuration
    /// (<c>APPLICATIONINSIGHTS_CONNECTION_STRING</c> / <c>ConnectionStrings:ApplicationInsights</c>).
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Fraction of traces exported to Azure Monitor (0.0–1.0). Azure Monitor bills per GB
    /// ingested — lower this in high-traffic services to control cost. Default: 1.0 (export all).
    /// Ignored when <see cref="TracesPerSecond"/> is set.
    /// </summary>
    public float SamplingRatio { get; set; } = 1.0f;

    /// <summary>
    /// Alternative cost control: caps exported traces at a fixed rate (e.g. 5.0/s) instead of a
    /// fraction — steadier bills under bursty traffic. When set, <see cref="SamplingRatio"/> is
    /// ignored. Default: null (ratio-based sampling).
    /// </summary>
    public double? TracesPerSecond { get; set; }

    /// <summary>
    /// Enables the Live Metrics stream (free of charge, but keeps an open channel). Default: true.
    /// </summary>
    public bool EnableLiveMetrics { get; set; } = true;

    /// <summary>
    /// Sets the OpenTelemetry resource attribute <c>service.name</c> (shown as the cloud role name).
    /// </summary>
    public string? ServiceName { get; set; }

    /// <summary>
    /// Sets the OpenTelemetry resource attribute <c>service.version</c>. Ignored when
    /// <see cref="ServiceName"/> is not set.
    /// </summary>
    public string? ServiceVersion { get; set; }

    /// <summary>
    /// Extra resource attributes attached to every span, metric and log record.
    /// </summary>
    public IDictionary<string, object> ResourceAttributes { get; } = new Dictionary<string, object>();

    /// <summary>
    /// Exports <c>ILogger</c> entries to Azure Monitor. Set to false to keep traces and metrics
    /// flowing while paying nothing for log ingestion. Default: true.
    /// </summary>
    public bool EnableLogExport { get; set; } = true;

    /// <summary>
    /// Minimum level of <c>ILogger</c> entries exported to Azure Monitor. Applies only to the
    /// export pipeline — console and other local providers keep their own verbosity.
    /// Default: <see cref="LogLevel.Information"/>; use <see cref="LogLevel.Warning"/> to cut cost.
    /// </summary>
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// Per-category overrides of <see cref="MinimumLogLevel"/> for the export pipeline —
    /// e.g. <c>["Microsoft.AspNetCore"] = LogLevel.Warning</c> silences framework noise while
    /// keeping application categories verbose.
    /// </summary>
    public IDictionary<string, LogLevel> CategoryLogLevels { get; } = new Dictionary<string, LogLevel>();

    /// <summary>
    /// Includes log scopes in exported entries. More context per entry, more bytes ingested.
    /// Default: false.
    /// </summary>
    public bool IncludeScopes { get; set; }

    /// <summary>
    /// Includes the rendered message alongside the template and placeholders. More readable in the
    /// portal, more bytes ingested. Default: false.
    /// </summary>
    public bool IncludeFormattedMessage { get; set; }
}
