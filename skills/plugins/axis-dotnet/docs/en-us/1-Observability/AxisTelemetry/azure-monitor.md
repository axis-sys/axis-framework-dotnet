# Azure Monitor adapter · `AxisTelemetry.AzureMonitor`

> The pairing package for Azure Monitor / Application Insights: registers the [OpenTelemetry adapter](opentelemetry-adapter.md) **and** wires the official distro `Azure.Monitor.OpenTelemetry.AspNetCore` (`UseAzureMonitor()`) in one call — Axis spans and metrics, ASP.NET Core / HttpClient instrumentation and `ILogger` export, all flowing to Application Insights.

```csharp
builder.Services.AddAzureMonitorAxis(builder.Configuration);
```

---

## When to use

Your service runs on (or reports to) Azure and you want traces, metrics and logs in Application Insights without assembling the OpenTelemetry pipeline yourself. `AxisTelemetry` deliberately ships no exporter — this package is exactly that missing pairing.

## When *not* to use

| You want to… | Use instead |
|---|---|
| export to any other sink (OTLP collector, Jaeger, Prometheus) | [`AddOpenTelemetryAxis`](opentelemetry-adapter.md) + the exporter of your choice |
| run tests without instrumentation cost | [`NullAxisTelemetry`](null-adapter.md) |

---

## Installation

```
dotnet add package AxisTelemetry.AzureMonitor
```

Depends on `AxisTelemetry` and `Azure.Monitor.OpenTelemetry.AspNetCore` (the official distro) — nothing else.

---

## What `AddAzureMonitorAxis(configuration)` does

1. Resolves the connection string, in order: `AzureMonitorAxisOptions.ConnectionString` → the `APPLICATIONINSIGHTS_CONNECTION_STRING` key (the App Insights standard environment variable) → the `ConnectionStrings:ApplicationInsights` entry (the standard .NET `GetConnectionString` idiom — no extra configuration section for clients).
2. **With** a connection string: calls `AddOpenTelemetryAxis()` (the `OpenTelemetryAdapter` singleton behind `IAxisTelemetry` + `IAxisMetrics`), then `AddOpenTelemetry().UseAzureMonitor(...)` subscribing the `"Axis.AxisMediator"` `ActivitySource` and `Meter` on top of the distro's ASP.NET Core / HttpClient instrumentation and `ILogger` export.
3. **Without** one: registers [`NullAxisTelemetry`](null-adapter.md), logs a startup warning, exports nothing — and **never throws**. Dev machines, CI and E2E tests boot the real `Program` with no App Insights around.

> **Host required for traces and logs.** The distro attaches the trace/log exporters when the host starts its `IHostedService`s (ASP.NET Core and the Generic Host do this for you). A hand-rolled `BuildServiceProvider()` that never starts hosted services exports metrics only.

```csharp
// Program.cs
builder.Services
    .AddAxisMediator()
    .AddAzureMonitorAxis(builder.Configuration, o =>
    {
        o.ServiceName = "orders-api";
        o.ServiceVersion = "2.0.1";
        o.SamplingRatio = 0.25f;                                   // export 25% of traces
        o.CategoryLogLevels["Microsoft.AspNetCore"] = LogLevel.Warning;
    });
```

---

## Cost — Azure Monitor bills per GB ingested

Application Insights charges by data volume (**~US$ 2.30/GB** on the default pay-as-you-go tier, with **5 GB/month free per Log Analytics workspace**). Two knobs control the bill:

- **`SamplingRatio`** — fraction of *traces* exported (0.0–1.0). A high-traffic API at `0.1` keeps distributed traces statistically useful at a tenth of the cost. **Sampling does not apply to logs.**
- **`TracesPerSecond`** — alternative to the ratio: caps exported traces at a fixed rate (steadier bills under bursty traffic). When set, `SamplingRatio` is ignored. The raw distro defaults to rate-limited sampling at 5 traces/s since 1.5; this package defaults to **ratio-based at 1.0** (deterministic — nothing silently dropped) and lets you opt into the rate limiter explicitly.
- **Log export options** — logs are usually the biggest cost driver; see below.

### Log export — cost vs verbosity, your call

Filters apply **only to the export pipeline** (`OpenTelemetryLoggerProvider`): console and other local providers keep their own verbosity — only what is ingested (billed) is trimmed.

| Option | Default | Effect |
|---|---|---|
| `EnableLogExport` | `true` | `false` → no `ILogger` entry reaches Azure Monitor (traces/metrics still flow) |
| `MinimumLogLevel` | `Information` | global floor for exported entries — `Warning` cuts cost sharply |
| `CategoryLogLevels` | empty | per-category overrides — silence `Microsoft.AspNetCore` noise, keep your app verbose |
| `IncludeScopes` | `false` | structured scopes per entry — more context, more bytes |
| `IncludeFormattedMessage` | `false` | rendered message besides the template — more readable, more bytes |

```csharp
// Minimal-cost profile
o.SamplingRatio = 0.1f;
o.MinimumLogLevel = LogLevel.Warning;
o.EnableLiveMetrics = false;

// Verbose-diagnostics profile
o.SamplingRatio = 1.0f;
o.MinimumLogLevel = LogLevel.Debug;
o.IncludeScopes = true;
o.IncludeFormattedMessage = true;
```

---

## `AzureMonitorAxisOptions` — the full surface

| Option | Default | Notes |
|---|---|---|
| `ConnectionString` | `null` | programmatic override of the configuration keys |
| `SamplingRatio` | `1.0f` | fraction of traces exported (ignored when `TracesPerSecond` is set) |
| `TracesPerSecond` | `null` | fixed-rate trace cap instead of a fraction |
| `EnableLiveMetrics` | `true` | the Live Metrics stream (free, keeps an open channel) |
| `ServiceName` / `ServiceVersion` | `null` | `service.name` / `service.version` resource attributes (cloud role name in the portal) |
| `ResourceAttributes` | empty | extra attributes stamped on every span, metric and log record |
| `EnableLogExport`, `MinimumLogLevel`, `CategoryLogLevels`, `IncludeScopes`, `IncludeFormattedMessage` | see above | the log cost/verbosity knobs |

---

## Relation to `AddOpenTelemetryAxis` and `NullAxisTelemetry`

`AddAzureMonitorAxis` **is** `AddOpenTelemetryAxis` plus the Azure Monitor exporter — application code keeps seeing only `IAxisTelemetry`/`IAxisMetrics`, so swapping Azure Monitor for an OTLP collector (or turning telemetry off) is a one-line DI change, never a call-site change. The no-connection-string fallback registers the same [`NullAxisTelemetry`](null-adapter.md) you would register by hand in tests.

---

## See also

- [OpenTelemetry adapter](opentelemetry-adapter.md) — the vendor-neutral half this package pairs with an exporter
- [Null adapter](null-adapter.md) — the graceful-degradation fallback
- [`TelemetryBehavior`](telemetry-behavior.md) — auto-instrument every mediator request
- [Getting started](getting-started.md) — the family walkthrough

---

↩ [Back to AxisTelemetry docs](README.md)
