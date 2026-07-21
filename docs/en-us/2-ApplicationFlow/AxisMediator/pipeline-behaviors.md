# Pipeline behaviours · `IAxisPipelineBehavior`

> Cross-cutting code that wraps every dispatch. Register an open-generic behaviour once; the mediator builds a chain around the handler.

```csharp
public interface IAxisPipelineBehavior<in TRequest> where TRequest : IAxisRequest
{
    Task<AxisResult> HandleAsync(TRequest request, AxisPipelineContext context, Func<Task<AxisResult>> next);
}

public interface IAxisPipelineBehavior<in TRequest, TResponse>
    where TRequest : IAxisRequest
    where TResponse : IAxisResponse
{
    Task<AxisResult<TResponse>> HandleAsync(TRequest request, AxisPipelineContext context, Func<Task<AxisResult<TResponse>>> next);
}
```

---

## When to use

Anything you would otherwise do "before every handler" or "after every handler": logging, validation, authorisation, telemetry, transactions, retries. The pipeline lets the handler stay focused on the use case.

## When *not* to use

| You want to… | Use instead |
|---|---|
| run **business logic** that depends on the request shape | the handler |
| call a port | inject the port into the handler |
| stream values from the behaviour | streams do not flow through `IAxisPipelineBehavior` — they bypass the pipeline |

---

## Anatomy of a behaviour

```csharp
public class MyBehavior<TRequest, TResponse>(IMyDep dep) : IAxisPipelineBehavior<TRequest, TResponse>
    where TRequest : IAxisRequest
    where TResponse : IAxisResponse
{
    public async Task<AxisResult<TResponse>> HandleAsync(
        TRequest request, AxisPipelineContext context, Func<Task<AxisResult<TResponse>>> next)
    {
        // pre — before the inner pipeline runs
        var result = await next();
        // post — after it ran
        return result;
    }
}
```

| Argument | Purpose |
|---|---|
| `request` | the typed request — same instance the handler receives |
| `context` | the [`AxisPipelineContext`](pipeline-context.md) — a per-call dictionary shared with other behaviours |
| `next` | a thunk that runs the rest of the pipeline (the next behaviour, or the handler) |

> Calling `next()` **once** is the contract. Skipping it (returning a short-circuit `Error`) is allowed and expected (validation does this). Calling it more than once is a programming error.

---

## Ordering

Reading `AxisMediatorHandler.ExecutePipelineAsync` directly: behaviours come from `IServiceProvider.GetServices<IAxisPipelineBehavior<TRequest>>().Reverse()` — and a `foreach` builds the chain. The result is:

```
Outermost (first registered)
   ┃
   ▼
   ... (others) ...
   ┃
   ▼
Innermost (last registered)
   ┃
   ▼
Handler
```

If you register `LoggingBehavior` then `ValidationBehavior`:

- The request enters `LoggingBehavior` (logs `Handling X`).
- Then `ValidationBehavior` (validates).
- Then the handler.
- Return path walks back out.

> The **first registered** behaviour sees the **request first** and the **response last**. Pick the order deliberately.

---

## In-box behaviours across the framework

| Behaviour | Package | What it does |
|---|---|---|
| `LoggingBehavior<TRequest>` / `<TRequest, TResponse>` | [`AxisLogger`](../../1-Observability/AxisLogger/README.md) | logs `Handling X` |
| `ValidationBehavior<TRequest>` / `<TRequest, TResponse>` | [`AxisValidator`](../AxisValidator/README.md) | short-circuits on `IAxisValidator<TRequest>.ValidateAsync` failure |
| `TelemetryBehavior<TRequest>` / `<TRequest, TResponse>` | [`AxisTelemetry`](../../1-Observability/AxisTelemetry/README.md) | wraps in a span, tags, records counters and histograms |
| `PerformanceBehavior<TRequest, TResponse>` | this package | warns when slow (>500ms) |

A typical wiring registers them in this order:

```csharp
services.AddLoggingBehavior();      // log first
services.AddAxisValidator(asm);     // validate next
services.AddOpenTelemetryAxis();
services.AddTransient(typeof(IAxisPipelineBehavior<,>), typeof(TelemetryBehavior<,>));
services.AddPerformanceBehavior();  // performance last (closest to the handler)
```

---

## Real-world examples

### 1. Authorisation behaviour

```csharp
public class AuthBehavior<TRequest, TResponse>(IAxisMediator mediator)
    : IAxisPipelineBehavior<TRequest, TResponse>
    where TRequest : IAxisRequest
    where TResponse : IAxisResponse
{
    public Task<AxisResult<TResponse>> HandleAsync(
        TRequest req, AxisPipelineContext ctx, Func<Task<AxisResult<TResponse>>> next)
    {
        if (req is IRequiresAuthentication && mediator.AxisEntityId is null)
            return Task.FromResult<AxisResult<TResponse>>(AxisError.Unauthorized("AUTH_REQUIRED"));

        return next();
    }
}
```

**Why it pays off:** every command/query that implements `IRequiresAuthentication` is checked once, at the pipeline level — handlers stop worrying about authentication entirely.

### 2. Transactional behaviour

```csharp
public class TransactionalBehavior<TRequest, TResponse>(IAxisUnitOfWork uow)
    : IAxisPipelineBehavior<TRequest, TResponse>
    where TRequest : IAxisCommand<TResponse>
    where TResponse : IAxisCommandResponse
{
    public Task<AxisResult<TResponse>> HandleAsync(
        TRequest req, AxisPipelineContext ctx, Func<Task<AxisResult<TResponse>>> next)
        => uow.InTransactionAsync(next);
}
```

**Why it pays off:** every command that has a typed response runs inside a transaction. Commit/rollback follows the railway. No `using var tx = …` boilerplate in any handler.

---

## See also

- [Pipeline context](pipeline-context.md) — share values between behaviours
- [Dispatching · `IAxisMediatorHandler`](dispatching.md) — how the chain is built
- [Performance behaviour](performance-behavior.md) — an in-box example
- [`LoggingBehavior`](../../1-Observability/AxisLogger/README.md) · [`ValidationBehavior`](../AxisValidator/README.md) · [`TelemetryBehavior`](../../1-Observability/AxisTelemetry/README.md)

---

↩ [Back to AxisMediator docs](README.md)
