# API reference

> The complete catalogue, grouped by responsibility. Use it for lookup — each group links back to its detail page.

---

## The contract — `IAxisLogger<T>`

| Method | Signature | Description |
|---|---|---|
| `LogDebug` | `void LogDebug(string, params (string Key, object? Value)[])` | structured debug entry |
| `LogInformation` | `void LogInformation(string, params (string Key, object? Value)[])` | structured information entry |
| `LogWarning` | `void LogWarning(string, params (string Key, object? Value)[])` | structured warning entry |
| `LogError` (no exception) | `void LogError(string, params (string Key, object? Value)[])` | structured error entry, no stack trace |
| `LogError` (with exception) | `void LogError(Exception, string, params (string Key, object? Value)[])` | structured error entry with stack trace |
| `LogCritical` | `void LogCritical(string, params (string Key, object? Value)[])` | structured critical entry |
| `LogResult` | `void LogResult(string tag, AxisResult result)` | log an `AxisResult` outcome; `Information` on success, `Error` on failure |

→ [The `IAxisLogger<T>` contract](iaxislogger.md) · [`LogResult`](log-result.md)

---

## Always-on enrichment

Every entry's scope carries:

| Key | Source |
|---|---|
| `UtcTime` | `TimeProvider.GetUtcNow().ToString("yyyy-MM-dd HH:mm:ss.fff zzz")` |
| `OriginId` | `IAxisMediator.OriginId` |
| `TraceId` | `IAxisMediator.TraceId` |
| `JourneyId` | `IAxisMediator.JourneyId` |

Plus your `(Key, Value)` properties (which overwrite the defaults if they share a key).

→ [Categories and structured properties](categories.md)

---

## Pipeline behaviour — `LoggingBehavior`

| Type | Where it sits | Method |
|---|---|---|
| `LoggingBehavior<TRequest>` | requests with no response | `HandleAsync(TRequest, AxisPipelineContext, Func<Task<AxisResult>>)` |
| `LoggingBehavior<TRequest, TResponse>` | requests with a typed response | `HandleAsync(TRequest, AxisPipelineContext, Func<Task<AxisResult<TResponse>>>)` |

Both log `"Handling {RequestName}"` at `Information` before calling `next()`.

→ [`LoggingBehavior` — automatic request logging](logging-behavior.md)

---

## DI extensions (C# 12 extensions on `IServiceCollection`)

| Extension | Effect |
|---|---|
| `AddAxisLogger()` | `services.AddLogging()` + `services.AddScoped(typeof(IAxisLogger<>), typeof(AxisLogger<>))` |
| `AddLoggingBehavior()` | `services.AddSingleton(TimeProvider.System)` + register `LoggingBehavior<>` and `LoggingBehavior<,>` as transient `IAxisPipelineBehavior` |

---

## `LogResult` behaviour table

| `result.IsSuccess` | `LogLevel` | Properties added |
|---|---|---|
| `true` | `Information` | `Tag`, `RequestName` |
| `false` | `Error` | `Tag`, `RequestName`, `AxisErrorList` |

Plus the always-on enrichment.

→ [`LogResult` — structured outcomes](log-result.md)

---

## Result-outcome tap extensions — `AxisResultLoggingExtensions`

Tap-family side-effect logging for `AxisResult`: log the outcome and return the SAME result unchanged, so the call slots into a `Then`/`Match` chain instead of a manual `if (result.IsFailure) logger.LogWarning(...)` check at every call site.

| Method | Signature | Description |
|---|---|---|
| `LogIfFailure` | `AxisResult LogIfFailure<T>(this AxisResult, IAxisLogger<T>, AxisFailureLogSeverity, string, params (string Key, object? Value)[])` | logs only on failure (`Warning` or `Error`, per severity); auto-appends the errors as `AxisErrorList`; returns the same result unchanged |
| `LogIfFailure` (typed) | `AxisResult<TValue> LogIfFailure<T, TValue>(this AxisResult<TValue>, IAxisLogger<T>, AxisFailureLogSeverity, string, params (string Key, object? Value)[])` | same as above, preserving `.Value` on the chain |
| `LogIfSuccess` | `AxisResult LogIfSuccess<T>(this AxisResult, IAxisLogger<T>, AxisSuccessLogSeverity, string, params (string Key, object? Value)[])` | logs only on success (`Information` or `Warning`, per severity); returns the same result unchanged |
| `LogIfSuccess` (typed) | `AxisResult<TValue> LogIfSuccess<T, TValue>(this AxisResult<TValue>, IAxisLogger<T>, AxisSuccessLogSeverity, string, params (string Key, object? Value)[])` | same as above, preserving `.Value` on the chain |

| Enum | Values | Used by |
|---|---|---|
| `AxisFailureLogSeverity` | `Warning`, `Error` | `LogIfFailure` |
| `AxisSuccessLogSeverity` | `Information`, `Warning` | `LogIfSuccess` |

---

## See also

- [Getting started](getting-started.md) — install, register, log
- [Why AxisLogger?](why-axislogger.md) — the case for the abstraction
- [Full documentation](README.md) — the map of the whole documentation

---

↩ [Back to AxisLogger docs](README.md)
