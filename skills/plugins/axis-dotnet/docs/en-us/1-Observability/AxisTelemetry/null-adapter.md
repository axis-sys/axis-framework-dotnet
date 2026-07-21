# Null adapter · `NullAxisTelemetry`

> A no-op implementation of `IAxisTelemetry` + `IAxisMetrics`, plus a no-op `NullAxisSpan`. Every method is empty, every span is the same shared instance. Use it to turn telemetry **off** without changing call sites.

```csharp
services.AddSingleton<IAxisTelemetry>(NullAxisTelemetry.Instance);
services.AddSingleton<IAxisMetrics>  (NullAxisTelemetry.Instance);
```

---

## When to use

- **Unit tests** that should not depend on or assert against the telemetry pipeline.
- **One-off CLI tools** where opening a span and a metric per call is pure overhead.
- **A migration phase** where you want the wiring in place but the telemetry off.

## When *not* to use

| You want to… | Use instead |
|---|---|
| ship to production | the [OpenTelemetry adapter](opentelemetry-adapter.md) |
| capture *some* signals (e.g. only metrics) | a custom adapter that delegates to the real one selectively |

---

## What it does

Reading `NullAxisTelemetry` directly:

| Method | Behaviour |
|---|---|
| `StartSpan(name, kind)` | returns `NullAxisSpan.Instance` |
| `CurrentTraceId` | `null` |
| `CurrentSpanId` | `null` |
| `RecordHistogram(name, value, tags)` | empty |
| `IncrementCounter(name, delta, tags)` | empty |

`NullAxisSpan`:

| Method | Behaviour |
|---|---|
| `TraceId` | `string.Empty` |
| `SpanId` | `string.Empty` |
| `SetTag(...)` | returns `this` |
| `SetStatus(...)` | returns `this` |
| `RecordException(...)` | returns `this` |
| `AddEvent(...)` | returns `this` |
| `Dispose()` | empty |

Both types expose a static `Instance` field; nothing allocates on the hot path.

---

## Real-world example — a clean unit-test DI graph

```csharp
public class FakeServiceFixture
{
    public IServiceProvider Build()
    {
        var services = new ServiceCollection();

        services
            .AddAxisMediator()
            .AddAxisLogger();

        services.AddSingleton<IAxisTelemetry>(NullAxisTelemetry.Instance);
        services.AddSingleton<IAxisMetrics>  (NullAxisTelemetry.Instance);

        // … the rest of the test wiring

        return services.BuildServiceProvider();
    }
}
```

**Why it pays off:** the handler under test still calls `telemetry.StartSpan(...)` and `metrics.IncrementCounter(...)`. The null adapter swallows everything; assertions stay focused on the application's behaviour, not on its telemetry plumbing.

---

## See also

- [The contracts](contracts.md) — what the adapter implements
- [OpenTelemetry adapter](opentelemetry-adapter.md) — the production counterpart
- [Spans · `IAxisSpan`](spans.md) — `NullAxisSpan` matches the interface

---

↩ [Back to AxisTelemetry docs](README.md)
