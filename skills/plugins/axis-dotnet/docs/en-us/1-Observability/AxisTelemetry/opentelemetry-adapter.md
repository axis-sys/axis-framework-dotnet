# OpenTelemetry adapter

> The bundled implementation of `IAxisTelemetry` + `IAxisMetrics` over `System.Diagnostics.ActivitySource` and `System.Diagnostics.Metrics.Meter`. Both are BCL primitives — the OpenTelemetry SDK consumes them, but so do other listeners (Application Insights, Sentry, your own collector).

```csharp
services.AddOpenTelemetryAxis();
```

---

## When to use

Production. Anywhere you want spans and metrics flowing to a sink. Pair with `OpenTelemetry.Extensions.Hosting` and the exporters of your choice to ship the data. For Azure Monitor / Application Insights, [`AxisTelemetry.AzureMonitor`](azure-monitor.md) ships this pairing ready-made.

## When *not* to use

| You want to… | Use instead |
|---|---|
| run tests without instrumentation cost | [`NullAxisTelemetry`](null-adapter.md) |
| write a different sink (Datadog SDK, raw HTTP) | a custom adapter implementing both contracts |

---

## What `AddOpenTelemetryAxis()` registers

Reading `DependencyInjection.AddOpenTelemetryAxis`:

```csharp
services.AddSingleton<OpenTelemetryAdapter>();
services.AddSingleton<IAxisTelemetry>(sp => sp.GetRequiredService<OpenTelemetryAdapter>());
services.AddSingleton<IAxisMetrics>  (sp => sp.GetRequiredService<OpenTelemetryAdapter>());
```

One singleton serves both ports. The adapter holds a process-wide `ActivitySource` and `Meter`, both named `"Axis.AxisMediator"`. Add them to your OpenTelemetry pipeline:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(b => b.AddSource("Axis.AxisMediator").AddOtlpExporter())
    .WithMetrics(b => b.AddMeter ("Axis.AxisMediator").AddOtlpExporter());
```

> The `SourceName` constant is `OpenTelemetryAdapter.SourceName = "Axis.AxisMediator"` — use the constant if you want to be sure it stays in sync.

---

## How each contract method maps to BCL primitives

| Contract | BCL call | Notes |
|---|---|---|
| `StartSpan(name, kind)` | `ActivitySource.StartActivity(name, MapKind(kind))` | wrapped in `ActivityAxisSpan(activity)` |
| `CurrentTraceId` | `Activity.Current?.TraceId.ToString()` | the W3C trace id |
| `CurrentSpanId` | `Activity.Current?.SpanId.ToString()` | the W3C span id |
| `RecordHistogram(name, value, tags)` | `_histograms.GetOrAdd(name, Meter.CreateHistogram<double>(n)).Record(value, tags)` | the histogram is created lazily and cached |
| `IncrementCounter(name, delta, tags)` | `_counters.GetOrAdd(name, Meter.CreateCounter<long>(n)).Add(delta, tags)` | the counter is created lazily and cached |

The lazy caching means you can call `RecordHistogram("my.metric", ...)` from any code path — the `Histogram<double>` is created on the first call and reused thereafter, atomically (concurrent-dictionary backed).

---

## `AxisSpanKind` → `ActivityKind`

| `AxisSpanKind` | `ActivityKind` |
|---|---|
| `Internal` | `Internal` |
| `Server` | `Server` |
| `Client` | `Client` |
| `Producer` | `Producer` |
| `Consumer` | `Consumer` |

---

## Real-world example — production wiring

```csharp
// Program.cs
builder.Services
    .AddAxisMediator()
    .AddOpenTelemetryAxis();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("orders-api"))
    .WithTracing(b => b
        .AddSource(OpenTelemetryAdapter.SourceName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(b => b
        .AddMeter(OpenTelemetryAdapter.SourceName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());
```

**Why it pays off:** the OpenTelemetry SDK exports your spans and metrics alongside the ASP.NET Core and HTTP-client instrumentation — one OTLP pipeline, one trace per request, with the `AxisMediator.{RequestName}` spans nested where they belong.

---

## See also

- [The contracts](contracts.md) — `IAxisTelemetry` and `IAxisMetrics`
- [Spans · `IAxisSpan`](spans.md) — `ActivityAxisSpan` is what `StartSpan` returns
- [Null adapter](null-adapter.md) — flip to no-ops in tests
- [`TelemetryBehavior`](telemetry-behavior.md) — the behaviour that feeds this adapter

---

↩ [Back to AxisTelemetry docs](README.md)
