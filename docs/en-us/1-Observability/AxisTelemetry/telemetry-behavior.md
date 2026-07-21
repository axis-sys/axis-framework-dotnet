# `TelemetryBehavior` — automatic instrumentation

> An opt-in `IAxisPipelineBehavior` that wraps every mediator request with an `AxisMediator.{RequestName}` span and records duration / invocation / exception metrics. One registration, everything traced.

```csharp
services.AddTransient(typeof(IAxisPipelineBehavior<>), typeof(TelemetryBehavior<>));
services.AddTransient(typeof(IAxisPipelineBehavior<,>), typeof(TelemetryBehavior<,>));
```

---

## When to use

Always — unless you have a reason not to. The behaviour is cheap (one span open / close per request, three metrics writes), and it gives you the per-handler view that you would otherwise have to wire by hand on every handler.

## When *not* to use

| You want to… | Use instead |
|---|---|
| skip telemetry entirely | wire `NullAxisTelemetry` instead, and skip this behaviour |
| record **business** metrics (orders / minute, revenue / day) | a custom behaviour or call `IAxisMetrics` from the handler |
| time only a subset of requests | a custom behaviour with a predicate |

---

## What the behaviour records

Reading `TelemetryBehavior<TRequest>` and `<TRequest, TResponse>` directly:

### The span

| Aspect | Value |
|---|---|
| Name | `$"AxisMediator.{typeof(TRequest).Name}"` |
| Kind | (default) `Internal` |
| `TelemetryTagNames.TraceId` | `mediator.TraceId` |
| `TelemetryTagNames.JourneyId` | `mediator.JourneyId` |
| `TelemetryTagNames.RequestType` | `"command"` (the non-generic overload) or `request is IAxisQuery ? "query" : "command"` (the typed overload) |
| `TelemetryTagNames.AxisEntityId` | `mediator.AxisEntityId` |
| `TelemetryTagNames.RequestName` | `typeof(TRequest).Name` |
| `TelemetryTagNames.ResultSuccess` | `result.IsSuccess` |
| `TelemetryTagNames.ErrorCodes` | comma-separated `result.Errors[*].Code` (on failure) |
| Status | `AxisSpanStatus.Ok` (success) / `AxisSpanStatus.Error` (failure) |
| Exception | `span.RecordException(ex)` (on unhandled exception) |

The span is also written into the pipeline `context` via `AxisPipelineContextKeys.Span`, so downstream behaviours can read it.

### The metrics

| Metric | Type | Tags |
|---|---|---|
| `axis.handler.duration_ms` | histogram (`double`) | `RequestName`, `ResultSuccess` |
| `axis.handler.invocations` | counter (`long`, delta 1) | `RequestName`, `ResultSuccess` |
| `axis.handler.exceptions` | counter (`long`, delta 1) | `RequestName`, `ExceptionType` |

The exception counter only increments when the handler **throws** (not when it returns `Error`). Returning a failed `AxisResult` only bumps `axis.handler.invocations` with `ResultSuccess = false`.

---

## Real-world example — what shows up at the sink

For a successful `CreateOrderCommand` taking 42ms:

```
span AxisMediator.CreateOrderCommand
    axis.trace_id      = …
    axis.journey_id    = …
    axis.request_type  = command
    axis.axis_entity_id = 1|01927a8b-…
    axis.request_name  = CreateOrderCommand
    axis.result_success = true
    status = Ok
metric axis.handler.duration_ms  = 42 (RequestName=CreateOrderCommand, ResultSuccess=true)
metric axis.handler.invocations  +1   (RequestName=CreateOrderCommand, ResultSuccess=true)
```

For a failed run (validation error, no exception):

```
span AxisMediator.CreateOrderCommand
    … same tags …
    axis.result_success = false
    axis.error_codes    = "PERSON_EMAIL_INVALID"
    status = Error
metric axis.handler.duration_ms  = 5  (RequestName=CreateOrderCommand, ResultSuccess=false)
metric axis.handler.invocations  +1   (RequestName=CreateOrderCommand, ResultSuccess=false)
```

For an exception (no `axis.error_codes` because the result was never returned):

```
span AxisMediator.CreateOrderCommand
    … same tags …
    event "exception" { type, message, stacktrace }
    status = Error
metric axis.handler.invocations  +1   (RequestName=CreateOrderCommand, ResultSuccess=false)
metric axis.handler.exceptions   +1   (RequestName=CreateOrderCommand, ExceptionType=DbUpdateException)
```

**Why it pays off:** dashboards can show p99 latency per command, failure rate per command, and exception count per type — without any per-handler instrumentation code.

---

## See also

- [The contracts](contracts.md) — the two ports the behaviour uses
- [Spans · `IAxisSpan`](spans.md) — the object the behaviour opens
- [Tag names](tag-names.md) — the constants tagged onto every span
- [OpenTelemetry adapter](opentelemetry-adapter.md) — what receives the spans/metrics

---

↩ [Back to AxisTelemetry docs](README.md)
