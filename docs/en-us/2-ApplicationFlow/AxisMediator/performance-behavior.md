# Performance behaviour · `PerformanceBehavior`

> An opt-in `IAxisPipelineBehavior<TRequest, TResponse>` that times the inner pipeline with a `Stopwatch` and emits an `IAxisLogger<TRequest>.LogWarning` when it exceeds **500ms**.

```csharp
services.AddPerformanceBehavior();
```

---

## When to use

Always, unless you have a more elaborate latency policy. The behaviour is cheap (one `Stopwatch` per request) and it gives you a structured warning per slow handler — easy to filter, easy to alert on.

## When *not* to use

| You want to… | Use instead |
|---|---|
| change the threshold | write your own behaviour (see below) |
| time **commands without a response** too | write your own — the in-box one only registers for `<TRequest, TResponse>` |
| record histograms / counters | `TelemetryBehavior` from [`AxisTelemetry`](../../1-Observability/AxisTelemetry/telemetry-behavior.md) — it already records `axis.handler.duration_ms` |

---

## What it does

Reading `PerformanceBehavior` directly:

```csharp
internal class PerformanceBehavior<TRequest, TResponse>(IAxisLogger<TRequest> logger)
    : IAxisPipelineBehavior<TRequest, TResponse>
    where TRequest : IAxisRequest
    where TResponse : IAxisResponse
{
    private const int SlowRequestThresholdMs = 500;

    public async Task<AxisResult<TResponse>> HandleAsync(
        TRequest request, AxisPipelineContext context, Func<Task<AxisResult<TResponse>>> next)
    {
        var sw = Stopwatch.StartNew();
        var response = await next();
        sw.Stop();

        if (sw.ElapsedMilliseconds > SlowRequestThresholdMs)
            logger.LogWarning($"Slow request: {typeof(TRequest).Name} took {sw.ElapsedMilliseconds}ms");

        return response;
    }
}
```

- The threshold is **500ms** (a `const int`).
- The warning is structured via `IAxisLogger<TRequest>` — the entry includes the auto-enriched `TraceId`/`OriginId`/`JourneyId`.
- Only the `<TRequest, TResponse>` overload exists — the void-command flavour is **not** timed by this behaviour.

---

## Registration

```csharp
builder.Services
    .AddAxisLogger()           // IAxisLogger<T>
    .AddPerformanceBehavior(); // PerformanceBehavior<,> registered as transient IAxisPipelineBehavior<,>
```

`AddPerformanceBehavior` registers the open generic `PerformanceBehavior<,>` against `IAxisPipelineBehavior<,>` — so every command-with-response and query gets it for free.

---

## Real-world example — a custom-threshold version

If 500ms is too sensitive (or too lax) for your domain, write your own:

```csharp
public class StrictPerformanceBehavior<TRequest, TResponse>(IAxisLogger<TRequest> logger)
    : IAxisPipelineBehavior<TRequest, TResponse>
    where TRequest : IAxisRequest
    where TResponse : IAxisResponse
{
    private const int ThresholdMs = 200;

    public async Task<AxisResult<TResponse>> HandleAsync(
        TRequest request, AxisPipelineContext context, Func<Task<AxisResult<TResponse>>> next)
    {
        var sw = Stopwatch.StartNew();
        var response = await next();
        sw.Stop();

        if (sw.ElapsedMilliseconds > ThresholdMs)
            logger.LogWarning("Slow request",
                ("RequestName", typeof(TRequest).Name),
                ("DurationMs", sw.ElapsedMilliseconds));

        return response;
    }
}

services.AddTransient(typeof(IAxisPipelineBehavior<,>), typeof(StrictPerformanceBehavior<,>));
```

**Why it pays off:** copy the pattern, set your own threshold, name your fields the way your sink expects. The behaviour is small enough to own.

---

## See also

- [Pipeline behaviours](pipeline-behaviors.md) — the general pattern
- [`TelemetryBehavior`](../../1-Observability/AxisTelemetry/telemetry-behavior.md) — records `axis.handler.duration_ms` for histograms
- [`AxisLogger`](../../1-Observability/AxisLogger/README.md) — the structured logger the warning uses

---

↩ [Back to AxisMediator docs](README.md)
