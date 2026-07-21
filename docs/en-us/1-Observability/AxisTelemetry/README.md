# AxisTelemetry — Documentation

> 🌐 [Português (documentação navegável)](../../../pt-br/1-Observability/AxisTelemetry/README.md)

**Tracing and metrics for the Axis pipeline** — `IAxisTelemetry` for spans, `IAxisMetrics` for counters and histograms, an `OpenTelemetryAdapter` over `System.Diagnostics.ActivitySource` + `Meter`, a `NullAxisTelemetry` for when you want telemetry off, and a `TelemetryBehavior` that wraps every mediator request with an `AxisMediator.{RequestName}` span plus duration / invocation / exception metrics.

```csharp
using var span = telemetry.StartSpan("db.postgres.commit", AxisSpanKind.Client);
span.SetTag("db.system", "postgresql");
try
{
    await transaction.CommitAsync();
    span.SetStatus(AxisSpanStatus.Ok);
}
catch (Exception ex)
{
    span.RecordException(ex);
    throw;
}
```

Use this page as a **map**: read the trunk below (~5 min) and jump straight to the detail of the group you need — without reading hundreds of lines.

---

## The trunk (read first)

### The two interfaces in 60 seconds

```csharp
public interface IAxisTelemetry
{
    IAxisSpan StartSpan(string operationName, AxisSpanKind kind = AxisSpanKind.Internal);
    string? CurrentTraceId { get; }
    string? CurrentSpanId { get; }
}

public interface IAxisMetrics
{
    void RecordHistogram(string name, double value, params KeyValuePair<string, object?>[] tags);
    void IncrementCounter(string name, long delta = 1, params KeyValuePair<string, object?>[] tags);
}
```

Tracing on one side, metrics on the other. The bundled `OpenTelemetryAdapter` implements both. → **[The contracts](contracts.md)**

### `IAxisSpan` — the span you actually work with

```csharp
public interface IAxisSpan : IDisposable
{
    string TraceId { get; }
    string SpanId { get; }
    IAxisSpan SetTag(string key, object? value);
    IAxisSpan SetStatus(AxisSpanStatus status, string? description = null);
    IAxisSpan RecordException(Exception exception);
    IAxisSpan AddEvent(string name, params KeyValuePair<string, object?>[] attributes);
}
```

Fluent — every mutator returns `this`. `Dispose()` ends the span. → **[Spans · `IAxisSpan`](spans.md)**

### `TelemetryBehavior` — auto-instrumentation of the mediator

Opt-in `IAxisPipelineBehavior` that opens an `AxisMediator.{RequestName}` span around every request, tags it with `TraceId`/`JourneyId`/`RequestType`/`AxisEntityId`/`RequestName`, times it, records:

- `axis.handler.duration_ms` (histogram)
- `axis.handler.invocations` (counter)
- `axis.handler.exceptions` (counter, on unhandled exceptions)

→ **[`TelemetryBehavior` — automatic instrumentation](telemetry-behavior.md)**

### Adapters

| Adapter | Use when |
|---|---|
| **`OpenTelemetryAdapter`** | production — emits to `ActivitySource` + `Meter`; pair with the OpenTelemetry exporter of your choice |
| **`AxisTelemetry.AzureMonitor`** (separate package) | production on Azure — the adapter above pre-paired with the official Azure Monitor / Application Insights distro, with cost controls (sampling, log filtering) and a no-op fallback when no connection string is around |
| **`NullAxisTelemetry`** | tests, single-process tools — every call is a no-op, every span is `NullAxisSpan.Instance` |

→ **[OpenTelemetry adapter](opentelemetry-adapter.md)** · **[Azure Monitor adapter](azure-monitor.md)** · **[Null adapter](null-adapter.md)**

### Installation

```
dotnet add package AxisTelemetry
```

`AxisTelemetry` depends directly only on `AxisLogger` (which brings `AxisResult` and `AxisMediator.Contracts` transitively). The OpenTelemetry adapter uses `System.Diagnostics.ActivitySource` and `System.Diagnostics.Metrics.Meter` from the BCL — no extra NuGet packages.

Exporting to Azure Monitor / Application Insights? Install the pairing package instead:

```
dotnet add package AxisTelemetry.AzureMonitor
```

→ **[Azure Monitor adapter](azure-monitor.md)**

→ Full guide: **[Getting started](getting-started.md)**

---

## The map (jump to what you need)

| Group | You want to… | Detail |
|---|---|---|
| **Contracts · `IAxisTelemetry` / `IAxisMetrics`** | the two ports | [contracts.md](contracts.md) |
| **Spans · `IAxisSpan`** ⭐ | start, tag, record exceptions, dispose | [spans.md](spans.md) |
| **`TelemetryBehavior`** | auto-trace every mediator request | [telemetry-behavior.md](telemetry-behavior.md) |
| **OpenTelemetry adapter** | production tracing + metrics | [opentelemetry-adapter.md](opentelemetry-adapter.md) |
| **Azure Monitor adapter** | export to Application Insights, control the bill | [azure-monitor.md](azure-monitor.md) |
| **Null adapter** | turn telemetry off in tests | [null-adapter.md](null-adapter.md) |
| **Tag names** | the canonical constants | [tag-names.md](tag-names.md) |
| **Why?** | the case for the abstraction | [why-axistelemetry.md](why-axistelemetry.md) |
| **Reference** | every member at a glance | [api-reference.md](api-reference.md) |

**Start here:** [Getting started](getting-started.md) · [The contracts](contracts.md) · [Why AxisTelemetry?](why-axistelemetry.md)

**Fundamentals:** [Spans · `IAxisSpan`](spans.md) · [`TelemetryBehavior`](telemetry-behavior.md) · [OpenTelemetry adapter](opentelemetry-adapter.md) · [Azure Monitor adapter](azure-monitor.md)

**Reference & extras:** [Null adapter](null-adapter.md) · [Tag names](tag-names.md) · [API reference](api-reference.md)

---

## Design principles

1. **Two narrow ports, one adapter.** `IAxisTelemetry` for traces, `IAxisMetrics` for metrics; the OpenTelemetry adapter implements both with one type.
2. **Vendor-neutral application code.** `ActivitySource` and `Meter` live in the BCL; the application sees `IAxisSpan` only.
3. **Pipeline-level instrumentation.** Register `TelemetryBehavior` and every command/query is timed, tagged and traced — no per-handler boilerplate.
4. **Null is a first-class adapter.** `NullAxisTelemetry.Instance` lets tests and one-off tools skip the cost without changing the calling code.
5. **Tag names are constants.** `TelemetryTagNames.RequestName` (and friends) prevent typos and make sink-side queries predictable.

---

## License

Apache 2.0
