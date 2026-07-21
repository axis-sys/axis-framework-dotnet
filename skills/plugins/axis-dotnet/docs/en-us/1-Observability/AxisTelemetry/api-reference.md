# API reference

> The complete catalogue, grouped by responsibility. Use it for lookup — each group links back to its detail page.

---

## Contracts

| Type | Members | Description |
|---|---|---|
| `IAxisTelemetry` | `IAxisSpan StartSpan(string, AxisSpanKind = Internal)`, `string? CurrentTraceId`, `string? CurrentSpanId` | tracing port |
| `IAxisMetrics` | `void RecordHistogram(string, double, params KeyValuePair<string, object?>[])`, `void IncrementCounter(string, long delta = 1, params KeyValuePair<string, object?>[])` | metrics port |

→ [The contracts](contracts.md)

---

## Span — `IAxisSpan`

| Member | Signature | Description |
|---|---|---|
| `TraceId` | `string` | the W3C trace id |
| `SpanId` | `string` | the W3C span id |
| `SetTag` | `IAxisSpan SetTag(string key, object? value)` | adds a structured tag |
| `SetStatus` | `IAxisSpan SetStatus(AxisSpanStatus status, string? description = null)` | sets the status |
| `RecordException` | `IAxisSpan RecordException(Exception)` | adds an `"exception"` event + `SetStatus(Error, ex.Message)` |
| `AddEvent` | `IAxisSpan AddEvent(string name, params KeyValuePair<string, object?>[] attributes)` | adds a point-in-time event |
| `Dispose` | `void Dispose()` | ends the span |

→ [Spans · `IAxisSpan`](spans.md)

---

## Enums

| Enum | Values |
|---|---|
| `AxisSpanKind` | `Internal`, `Server`, `Client`, `Producer`, `Consumer` |
| `AxisSpanStatus` | `Unset`, `Ok`, `Error` |

---

## Adapters

| Type | Implements | Description |
|---|---|---|
| `OpenTelemetryAdapter` | `IAxisTelemetry`, `IAxisMetrics` | wraps `ActivitySource("Axis.AxisMediator")` and `Meter("Axis.AxisMediator")` |
| `OpenTelemetryAdapter.SourceName` | `const string` = `"Axis.AxisMediator"` | the shared source/meter name |
| `NullAxisTelemetry` | `IAxisTelemetry`, `IAxisMetrics` | no-op; `NullAxisTelemetry.Instance` |
| `NullAxisSpan` | `IAxisSpan` | no-op span; `NullAxisSpan.Instance` |
| `ActivityAxisSpan` (internal) | `IAxisSpan` | the `Activity`-backed implementation |
| `AzureMonitorDisabledWarning` (internal, `AxisTelemetry.AzureMonitor`) | `IHostedService` | startup warning when no connection string is found and telemetry degraded to `NullAxisTelemetry` |

The `AxisTelemetry.AzureMonitor` package adds no new adapter type — it pairs `OpenTelemetryAdapter` with the official Azure Monitor distro (falling back to `NullAxisTelemetry` without a connection string).

→ [OpenTelemetry adapter](opentelemetry-adapter.md) · [Null adapter](null-adapter.md) · [Azure Monitor adapter](azure-monitor.md)

---

## Pipeline behaviour — `TelemetryBehavior`

| Type | Where it sits | What it does |
|---|---|---|
| `TelemetryBehavior<TRequest>` | requests without a typed response | opens an `AxisMediator.{TRequest.Name}` span; tags `TraceId`/`JourneyId`/`RequestType`/`AxisEntityId`/`RequestName`; records `axis.handler.duration_ms`, `axis.handler.invocations`, `axis.handler.exceptions` |
| `TelemetryBehavior<TRequest, TResponse>` | requests with a typed response | same, plus `RequestType = request is IAxisQuery ? "query" : "command"` |

→ [`TelemetryBehavior`](telemetry-behavior.md)

---

## Tag-name constants

### `TelemetryTagNames`

`AxisEntityId` · `TraceId` · `JourneyId` · `RequestType` · `RequestName` · `ResultSuccess` · `ErrorCodes` · `ExceptionType`

### `AuthTelemetryTagNames`

`Scheme` · `Result` · `FailureReason` · `ApiId` · `BruteForceSuspected`

→ [Tag names](tag-names.md)

---

## Recorded metrics (by `TelemetryBehavior`)

| Metric | Type | Tags |
|---|---|---|
| `axis.handler.duration_ms` | histogram | `RequestName`, `ResultSuccess` |
| `axis.handler.invocations` | counter | `RequestName`, `ResultSuccess` |
| `axis.handler.exceptions` | counter | `RequestName`, `ExceptionType` |

→ [`TelemetryBehavior`](telemetry-behavior.md)

---

## DI extension

| Extension | Effect |
|---|---|
| `services.AddOpenTelemetryAxis()` | registers `OpenTelemetryAdapter` (singleton) and binds both `IAxisTelemetry` and `IAxisMetrics` to it |
| `services.AddAzureMonitorAxis(IConfiguration, Action<AzureMonitorAxisOptions>? = null)` (`AxisTelemetry.AzureMonitor`) | with a connection string: `AddOpenTelemetryAxis()` + `AddOpenTelemetry().UseAzureMonitor(...)` subscribing `"Axis.AxisMediator"`; without one: `NullAxisTelemetry` on both ports + a startup warning — never throws |

---

## Options — `AzureMonitorAxisOptions` (`AxisTelemetry.AzureMonitor`)

| Property | Type | Default | Effect |
|---|---|---|---|
| `ConnectionString` | `string?` | `null` | programmatic override of `APPLICATIONINSIGHTS_CONNECTION_STRING` / `ConnectionStrings:ApplicationInsights` |
| `SamplingRatio` | `float` | `1.0f` | fraction of traces exported (0.0–1.0); ignored when `TracesPerSecond` is set |
| `TracesPerSecond` | `double?` | `null` | fixed-rate trace cap instead of a fraction; when set, `SamplingRatio` is ignored |
| `EnableLiveMetrics` | `bool` | `true` | the Live Metrics stream (free, keeps an open channel) |
| `ServiceName` | `string?` | `null` | `service.name` resource attribute (cloud role name in the portal) |
| `ServiceVersion` | `string?` | `null` | `service.version` resource attribute; ignored when `ServiceName` is not set |
| `ResourceAttributes` | `IDictionary<string, object>` | empty | extra attributes stamped on every span, metric and log record |
| `EnableLogExport` | `bool` | `true` | `false` → no `ILogger` entry reaches Azure Monitor (traces/metrics still flow) |
| `MinimumLogLevel` | `LogLevel` | `Information` | floor for exported entries — export pipeline only, local providers untouched |
| `CategoryLogLevels` | `IDictionary<string, LogLevel>` | empty | per-category overrides of `MinimumLogLevel` for the export pipeline |
| `IncludeScopes` | `bool` | `false` | log scopes in exported entries — more context, more bytes |
| `IncludeFormattedMessage` | `bool` | `false` | rendered message besides the template — more readable, more bytes |

→ [Azure Monitor adapter](azure-monitor.md)

---

## See also

- [Getting started](getting-started.md) — install, register, trace
- [Why AxisTelemetry?](why-axistelemetry.md) — the case for the abstraction
- [Full documentation](README.md) — the map of the whole documentation

---

↩ [Back to AxisTelemetry docs](README.md)
