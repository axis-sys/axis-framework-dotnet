# Contract · `IAxisLogger<T>`

> Six familiar log levels plus a railway-aware `LogResult`. Every overload accepts `params (string Key, object? Value)[] properties` for structured enrichment. Every entry is wrapped in an `ILogger.BeginScope(...)` carrying `UtcTime`, `OriginId`, `TraceId`, `JourneyId` and your properties.

```csharp
public interface IAxisLogger<in T>
{
    void LogDebug(string message, params (string Key, object? Value)[] properties);
    void LogInformation(string message, params (string Key, object? Value)[] properties);
    void LogWarning(string message, params (string Key, object? Value)[] properties);
    void LogError(string message, params (string Key, object? Value)[] properties);
    void LogError(Exception exception, string message, params (string Key, object? Value)[] properties);
    void LogCritical(string message, params (string Key, object? Value)[] properties);
    void LogResult(string tag, AxisResult result);
}
```

---

## When to use

Inject `IAxisLogger<MyClass>` instead of `ILogger<MyClass>` in any code that runs inside the Axis pipeline — handlers, behaviours, adapters, services. The `T` is the category; the structured props are how you describe *what* happened.

## When *not* to use

| You want to… | Use instead |
|---|---|
| log from infrastructure code that runs **outside** an `IAxisMediator` scope (background workers without a mediator) | `ILogger<T>` directly |
| build interpolated, human-only messages | nothing — but pass values as structured properties, not as `$"…{x}…"` |
| emit metrics or traces (not logs) | [`AxisTelemetry`](../AxisTelemetry/README.md) |

---

## What every overload does

Reading `AxisLogger<T>` directly:

| Method | `LogLevel` | Exception | Notes |
|---|---|---|---|
| `LogDebug` | `Debug` | — | suppressed unless `Debug` is enabled at the sink |
| `LogInformation` | `Information` | — | the default for "this is what happened" |
| `LogWarning` | `Warning` | — | something off, recoverable |
| `LogError` (string overload) | `Error` | — | something failed but did not throw |
| `LogError` (exception overload) | `Error` | the exception | for catches at the boundary |
| `LogCritical` | `Critical` | — | something fundamental broke |
| `LogResult` | `Information` or `Error` | — | level chosen by `result.IsSuccess`; see [`LogResult`](log-result.md) |

Every method goes through `Write(level, exception?, message, properties)`, which:

1. Skips the call entirely if `logger.IsEnabled(level)` is `false`.
2. Calls `logger.BeginScope(BuildScope(properties))`.
3. Calls `logger.Log(level, [exception], message)`.

---

## What `BuildScope` puts on the entry

Always present:

| Key | Source |
|---|---|
| `UtcTime` | `TimeProvider.GetUtcNow().ToString("yyyy-MM-dd HH:mm:ss.fff zzz")` |
| `OriginId` | `mediator.OriginId` — the upstream system that initiated the journey |
| `TraceId` | `mediator.TraceId` — the per-request trace correlation |
| `JourneyId` | `mediator.JourneyId` — the saga/long-running journey id (if any) |

Plus every `(Key, Value)` pair you passed. Your properties **overwrite** the defaults if they share a key.

---

## Real-world examples

### 1. Adding context to a single entry

```csharp
logger.LogInformation("Order created",
    ("OrderId",    order.OrderId),
    ("CustomerId", order.CustomerId),
    ("Total",      order.Total));
```

**Why it pays off:** the sink sees three structured fields side-by-side with `TraceId`/`OriginId`/`JourneyId`. Filtering "all orders for customer X with total > 1000" is a query, not a regex.

### 2. Logging a caught exception at the boundary

```csharp
try
{
    return await httpClient.GetFromJsonAsync<Person>(url);
}
catch (HttpRequestException ex)
{
    logger.LogError(ex, "Upstream lookup failed", ("Url", url));
    return AxisError.ServiceUnavailable("PERSON_LOOKUP_UNAVAILABLE");
}
```

**Why it pays off:** the stack trace lives in the entry, the URL is a structured property (not jammed into the message), and `TraceId` carries from there through the rest of the request.

### 3. Tagging a metric-style entry

```csharp
logger.LogInformation("Webhook received",
    ("Provider",  "stripe"),
    ("EventType", payload.Type),
    ("Tenant",    tenant));
```

**Why it pays off:** the sink (Serilog/OpenTelemetry/Datadog) can roll up event counts by `Provider`/`EventType`/`Tenant` without parsing strings.

---

## See also

- [`LogResult` — structured outcomes](log-result.md) — the railway companion
- [`LoggingBehavior` — automatic request logging](logging-behavior.md) — opt-in pipeline behaviour
- [Categories and structured properties](categories.md) — what `T` actually does
- [API reference](api-reference.md) — every member, in one place

---

↩ [Back to AxisLogger docs](README.md)
