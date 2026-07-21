# The contracts · `IAxisTelemetry`, `IAxisMetrics`

> Two narrow ports — `IAxisTelemetry` for spans, `IAxisMetrics` for counters and histograms. The bundled `OpenTelemetryAdapter` implements both; the bundled `NullAxisTelemetry` implements both as no-ops.

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

---

## When to use

`IAxisTelemetry` whenever you want a **span** around a unit of work (`StartSpan` returns an `IAxisSpan` you tag and dispose). `IAxisMetrics` whenever you want a **counter** or a **histogram** — invocations per handler, latency, cache hit rates.

## When *not* to use

| You want to… | Use instead |
|---|---|
| log a message | [`AxisLogger`](../AxisLogger/README.md) |
| publish a domain event | [`AxisBus`](../../2-ApplicationFlow/AxisBus/README.md) |
| auto-instrument every mediator request | [`TelemetryBehavior`](telemetry-behavior.md) — calls these contracts under the hood |

---

## `AxisSpanKind` — the five kinds

| Value | When | OpenTelemetry equivalent |
|---|---|---|
| `Internal` (default) | in-process work (handler, validator) | `ActivityKind.Internal` |
| `Server` | inbound HTTP / gRPC request | `ActivityKind.Server` |
| `Client` | outbound HTTP / DB / external call | `ActivityKind.Client` |
| `Producer` | publishing a message to a bus | `ActivityKind.Producer` |
| `Consumer` | consuming a message from a bus | `ActivityKind.Consumer` |

The kind drives downstream tooling (waterfalls, service graphs). Pick the one that matches the operation.

---

## What `IAxisTelemetry.CurrentTraceId` / `CurrentSpanId` return

| Adapter | `CurrentTraceId` | `CurrentSpanId` |
|---|---|---|
| `OpenTelemetryAdapter` | `Activity.Current?.TraceId.ToString()` | `Activity.Current?.SpanId.ToString()` |
| `NullAxisTelemetry` | `null` | `null` |

These are useful for log enrichment in your own adapters, or for stamping a span id onto a response header.

---

## Real-world examples

### 1. Trace a downstream HTTP call

```csharp
public async Task<AxisResult<Person>> GetUpstreamAsync(AxisEntityId id)
{
    using var span = telemetry.StartSpan("http.lookup.person", AxisSpanKind.Client);
    span.SetTag("http.url", $"https://upstream/person/{id}");

    var response = await httpClient.GetAsync($"/person/{id}");
    span.SetTag("http.status_code", (int)response.StatusCode);

    if (!response.IsSuccessStatusCode)
    {
        span.SetStatus(AxisSpanStatus.Error, $"HTTP {(int)response.StatusCode}");
        return AxisError.ServiceUnavailable("PERSON_LOOKUP_UNAVAILABLE");
    }

    span.SetStatus(AxisSpanStatus.Ok);
    return await response.Content.ReadFromJsonAsync<Person>();
}
```

**Why it pays off:** the span and its tags travel as one structured object; the call shows up in your trace UI as a `Client` span with HTTP method, URL and status code, ready to query.

### 2. Histogram of cache lookups

```csharp
public async Task<AxisResult<T?>> GetAsync<T>(string key)
{
    var sw = Stopwatch.StartNew();
    var result = await innerCache.GetAsync<T>(key);
    sw.Stop();

    metrics.RecordHistogram("cache.lookup_ms", sw.Elapsed.TotalMilliseconds,
        new("cache.key_prefix", key.Split(':')[0]),
        new("cache.hit", result.IsSuccess && result.Value is not null));

    return result;
}
```

**Why it pays off:** the histogram is partitioned by prefix and hit/miss; Prometheus / Grafana can graph p99 lookup time by prefix, hit rate by prefix, and alert when a particular prefix degrades.

### 3. Counter for an event you care about

```csharp
public async Task<AxisResult> HandleAsync(WebhookReceivedEvent @event)
{
    metrics.IncrementCounter("webhook.received",
        new("webhook.provider", @event.Provider),
        new("webhook.type",     @event.Type));

    return await processor.HandleAsync(@event);
}
```

**Why it pays off:** the counter is multi-dimensional from day one — slicing by provider or type is a sink-side query, not a code change.

---

## See also

- [Spans · `IAxisSpan`](spans.md) — the object `StartSpan` returns
- [`TelemetryBehavior`](telemetry-behavior.md) — uses both contracts automatically
- [OpenTelemetry adapter](opentelemetry-adapter.md) — the in-box implementation
- [Tag names](tag-names.md) — the canonical constants

---

↩ [Back to AxisTelemetry docs](README.md)
