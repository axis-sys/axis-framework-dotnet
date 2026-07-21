# `LoggingBehavior` — automatic request logging

> An opt-in `IAxisPipelineBehavior` that logs `"Handling {RequestName}"` at the top of every mediator request. Register it once; every handler in your app benefits.

```csharp
services
    .AddAxisLogger()
    .AddLoggingBehavior();   // wires LoggingBehavior<TRequest> and <TRequest, TResponse>
```

---

## When to use

Always, unless you have a reason not to. Per-request "Handling X" log entries are the cheapest cross-cutting observability gain available — and they pair perfectly with `LogResult` at the bottom of the pipeline.

## When *not* to use

| You want to… | Use instead |
|---|---|
| only log a subset of handlers | inject `IAxisLogger<T>` per handler and call it manually |
| log the **payload** of the request | write your own behaviour with redaction |
| log latency / metrics | [`AxisTelemetry`](../AxisTelemetry/README.md) |

---

## What the behaviour does

Reading `LoggingBehavior<TRequest>` and `<TRequest, TResponse>` directly:

| Variant | Where it sits | What it logs |
|---|---|---|
| `LoggingBehavior<TRequest>` | requests with no response | `LogInformation("Handling {RequestName}.", ("RequestName", typeof(TRequest).Name))` |
| `LoggingBehavior<TRequest, TResponse>` | requests with a typed response | same shape, same call |

After the log call, the behaviour just `await`s `next()` — it does **not** wrap the call in `try/catch`, does **not** log the outcome, does **not** time anything. Pair with `LogResult` (manual or via your own behaviour) for the outcome side.

---

## What gets registered

`AddLoggingBehavior` (from `DependencyInjection.cs`):

```csharp
public IServiceCollection AddLoggingBehavior()
{
    services.AddSingleton(TimeProvider.System);
    services.AddTransient(typeof(IAxisPipelineBehavior<>),  typeof(LoggingBehavior<>));
    services.AddTransient(typeof(IAxisPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    return services;
}
```

- `TimeProvider.System` is registered as a singleton so `IAxisLogger<T>`'s `UtcTime` enrichment works.
- Two **open-generic** behaviours are registered as transient so the mediator can resolve one per request type.

---

## Real-world example — composing with a manual `LogResult`

```csharp
// Program.cs
builder.Services
    .AddAxisMediator()
    .AddAxisLogger()
    .AddLoggingBehavior();    // logs Handling X at the top

// Handler
public class CreatePersonHandler(IAxisLogger<CreatePersonHandler> logger, ...)
{
    public Task<AxisResult<CreatePersonResponse>> HandleAsync(CreatePersonCommand cmd)
        => factory.CreateAsync(cmd)
            .ThenAsync(p => uow.SaveChangesAsync())
            .TapAsync(r => logger.LogResult("CreatePerson", r))   // logs the outcome at the bottom
            .MapAsync(_ => new CreatePersonResponse { … });
}
```

**Why it pays off:** the request-level "Handling …" entry is automatic; the per-handler outcome entry is one line. Together they bracket every request with `TraceId`/`OriginId`/`JourneyId` — the timeline reads cleanly in any sink.

---

## See also

- [The `IAxisLogger<T>` contract](iaxislogger.md) — what the behaviour ends up calling
- [`LogResult`](log-result.md) — pair with the behaviour at the end of the pipeline
- [Categories and structured properties](categories.md) — why the behaviour uses `IAxisLogger<TRequest>`

---

↩ [Back to AxisLogger docs](README.md)
