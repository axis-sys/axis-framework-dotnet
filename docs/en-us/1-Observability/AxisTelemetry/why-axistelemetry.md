# Why AxisTelemetry? · comparison

> There are other ways to instrument .NET code. This page tells you why AxisTelemetry is different — a direct comparison, no hand-waving.

---

## vs. `ActivitySource` / `Meter` directly

`System.Diagnostics.ActivitySource` and `System.Diagnostics.Metrics.Meter` are the substrates. AxisTelemetry uses them. Calling them directly from handlers has three rough edges:

1. **Boilerplate.** `var activity = source.StartActivity(...)` → `try/finally` → tag → set status → `Dispose()` — at every site.
2. **No `Result`-aware status.** Translating an `AxisResult` to `ActivityStatusCode` is your problem at every call site.
3. **Mediator integration is your problem.** No automatic `TraceId`/`JourneyId` tags, no per-handler timing, no exception counters.

**AxisTelemetry** moves all of that into `TelemetryBehavior` plus the fluent `IAxisSpan`. Production code stays focused on the business operation.

## vs. `OpenTelemetry.Trace.Tracer` (the SDK type)

The OpenTelemetry SDK's `Tracer` is similar in shape, but tightly coupled to the SDK package. AxisTelemetry stays on the BCL types so a different sink (App Insights via `ApplicationInsights.WorkerService`, raw OTLP, Datadog SDK) can listen without re-wiring application code.

## vs. a bespoke `IInstrumentation`

DIY. Same shape, but you re-derive: the `Span` interface, the `Null` adapter, the `Pipeline behaviour`, the tag-name constants. `AxisTelemetry` saves the cost — and is designed alongside `AxisMediator`, `AxisLogger`, `AxisRepository` so the auto-instrumentation lights up across packages.

---

## The comparison

| Feature | AxisTelemetry | `ActivitySource` direct | OpenTelemetry SDK direct | Bespoke `IInstrumentation` |
|---|:--:|:--:|:--:|:--:|
| Fluent `IAxisSpan` | **Yes** | No (manual `Activity.SetTag`) | No (manual `TelemetrySpan`) | Maybe |
| `IAxisMetrics` for counters / histograms | **Yes** | No (raw `Meter`/`Counter<T>`) | Yes | Maybe |
| Auto pipeline behaviour (`TelemetryBehavior`) | **Yes** | No | No | Maybe |
| `NullAxisTelemetry` for tests | **Yes** | n/a | n/a | Maybe |
| Tag-name constants | **Yes** | No | No | Maybe |
| BCL primitives only (no NuGet beyond BCL) | **Yes** | Yes | No | Yes |
| `AxisResult`-aware status | **Yes** | No | No | Maybe |

---

## See also

- [The contracts](contracts.md) — the surface
- [`TelemetryBehavior`](telemetry-behavior.md) — the operator that justifies the abstraction
- [OpenTelemetry adapter](opentelemetry-adapter.md) — the in-box implementation

---

↩ [Back to AxisTelemetry docs](README.md)
