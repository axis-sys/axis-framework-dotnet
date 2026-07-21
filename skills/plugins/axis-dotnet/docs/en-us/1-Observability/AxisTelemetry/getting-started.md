# Getting started · installation and usage

> Install the package, register the OpenTelemetry adapter, plug the behaviour into the mediator, and start a span — in five minutes.

---

## Installation

```
dotnet add package AxisTelemetry
```

The package depends directly only on `AxisLogger` (which brings `AxisResult` and `AxisMediator.Contracts` transitively). The OpenTelemetry adapter uses `System.Diagnostics.ActivitySource` and `System.Diagnostics.Metrics.Meter` from the BCL.

---

## Registering

```csharp
using Axis;

builder.Services
    .AddAxisMediator()
    .AddOpenTelemetryAxis();    // wires OpenTelemetryAdapter as IAxisTelemetry + IAxisMetrics

// then expose the Axis ActivitySource and Meter to your OpenTelemetry pipeline
builder.Services.AddOpenTelemetry()
    .WithTracing(b => b.AddSource("Axis.AxisMediator").AddOtlpExporter())
    .WithMetrics(b => b.AddMeter ("Axis.AxisMediator").AddOtlpExporter());
```

`AddOpenTelemetryAxis()`:

- Registers `OpenTelemetryAdapter` as a singleton.
- Binds `IAxisTelemetry` and `IAxisMetrics` to the same instance.

The adapter uses an internal `ActivitySource` and `Meter` both named `"Axis.AxisMediator"`. Add them to your OpenTelemetry pipeline (or whatever sink consumes `Activity`/`Meter`) for the spans and metrics to flow out.

> **Shipping to Azure Monitor / Application Insights?** The pairing package `AxisTelemetry.AzureMonitor` replaces both registrations above with a single `AddAzureMonitorAxis(builder.Configuration)` — adapter, exporter and `ILogger` export in one call. See the [Azure Monitor adapter](azure-monitor.md).

---

## Starting a span

```csharp
public Task<AxisResult> CommitAsync()
{
    using var span = telemetry.StartSpan("db.postgres.commit", AxisSpanKind.Client);
    span.SetTag("db.system", "postgresql");

    try
    {
        await transaction.CommitAsync(ct);
        span.SetStatus(AxisSpanStatus.Ok);
        return AxisResult.Ok();
    }
    catch (Exception ex)
    {
        span.RecordException(ex);
        return AxisError.InternalServerError("POSTGRES_COMMIT_ERROR");
    }
}
```

`using var` ensures the span ends when the block exits.

---

## Auto-instrumenting the mediator

Register `TelemetryBehavior` as an `IAxisPipelineBehavior`:

```csharp
services.AddTransient(typeof(IAxisPipelineBehavior<>), typeof(TelemetryBehavior<>));
services.AddTransient(typeof(IAxisPipelineBehavior<,>), typeof(TelemetryBehavior<,>));
```

Now every mediator request gets:

- a span named `AxisMediator.{RequestName}` with `TraceId`/`JourneyId`/`RequestType`/`AxisEntityId`/`RequestName` tags;
- a `axis.handler.duration_ms` histogram;
- an `axis.handler.invocations` counter;
- a `axis.handler.exceptions` counter (when something throws).

**Why it pays off:** the timing, tracing and counters cover every handler in the system — *one* registration line replaces every per-handler `using var span = …`.

---

## Tests — turn telemetry off

```csharp
services.AddSingleton<IAxisTelemetry>(NullAxisTelemetry.Instance);
services.AddSingleton<IAxisMetrics>  (NullAxisTelemetry.Instance);
```

`NullAxisTelemetry` is a no-op: every `StartSpan` returns `NullAxisSpan.Instance`, every metrics method is empty. Cheap, allocation-free, makes unit tests blind to telemetry.

---

## See also

- [The contracts](contracts.md) — `IAxisTelemetry` and `IAxisMetrics` in depth
- [Spans · `IAxisSpan`](spans.md) — tag, record exceptions, add events
- [`TelemetryBehavior`](telemetry-behavior.md) — what the auto-instrumentation actually records
- [OpenTelemetry adapter](opentelemetry-adapter.md) — `ActivitySource` / `Meter` plumbing
- [Azure Monitor adapter](azure-monitor.md) — the one-call pairing with Application Insights
- [Null adapter](null-adapter.md) — turn it off in tests
- [Tag names](tag-names.md) — the canonical constants
- [Why AxisTelemetry?](why-axistelemetry.md) — the case against `ActivitySource` directly
- [API reference](api-reference.md) — every member in one place

---

↩ [Back to AxisTelemetry docs](README.md)
