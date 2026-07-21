# Dispatching · `IAxisMediatorHandler`

> Four methods on `mediator.Cqrs` — one per request shape. The dispatcher resolves the handler, builds the pipeline, runs it, and (for `AxisResult`-returning shapes) logs the outcome.

```csharp
public interface IAxisMediatorHandler
{
    Task<AxisResult>             ExecuteAsync<TCommand>(TCommand command);
    Task<AxisResult<TResponse>>  ExecuteAsync<TCommand, TResponse>(TCommand command);
    Task<AxisResult<TResponse>>  QueryAsync<TQuery, TResponse>(TQuery query);
    IAsyncEnumerable<TItem>      StreamAsync<TQuery, TItem>(TQuery query);
}
```

---

## When to use

Any code path that needs to invoke a handler — controllers, edge middlewares, integration handlers, even other handlers (sparingly). The dispatcher is the **only** way handlers run inside the pipeline; calling `handler.HandleAsync(...)` directly bypasses every behaviour.

## When *not* to use

| You want to… | Use instead |
|---|---|
| publish an event | [`AxisBus.PublishAsync`](../AxisBus/README.md) |
| call a port (your own interface) | inject the port; it is not a request shape |
| span the call across services | a [`Saga`](../AxisSaga/README.md) or an out-of-process invocation |

---

## The four methods

| Method | Constraints | Returns | Handler interface |
|---|---|---|---|
| `ExecuteAsync<TCommand>(command)` | `TCommand : IAxisCommand` | `Task<AxisResult>` | `IAxisCommandHandler<TCommand>` |
| `ExecuteAsync<TCommand, TResponse>(command)` | `TCommand : IAxisCommand<TResponse>`, `TResponse : IAxisCommandResponse` | `Task<AxisResult<TResponse>>` | `IAxisCommandHandler<TCommand, TResponse>` |
| `QueryAsync<TQuery, TResponse>(query)` | `TQuery : IAxisQuery<TResponse>`, `TResponse : IAxisQueryResponse` | `Task<AxisResult<TResponse>>` | `IAxisQueryHandler<TQuery, TResponse>` |
| `StreamAsync<TQuery, TItem>(query)` | `TQuery : IAxisStreamQuery<TItem>` | `IAsyncEnumerable<TItem>` | `IAxisStreamQueryHandler<TQuery, TItem>` |

---

## What the dispatcher does

Reading `AxisMediatorHandler` directly:

1. **Resolve the handler** from `IServiceProvider`. If it cannot be found:
   - `ExecuteAsync` / `QueryAsync` → `AxisError.NotFound($"HANDLER_NOT_FOUND_{typeof(TRequest).Name}")`.
   - `StreamAsync` → throws `InvalidOperationException` with the same message (you cannot return an error from `IAsyncEnumerable<TItem>` here).
2. **Build the pipeline** from `IServiceProvider.GetServices<IAxisPipelineBehavior<...>>()` — reversed so the **first registered** behaviour is the outermost wrapper.
3. **Run the pipeline**, then call `LogResult<TRequest>` (success → `LogInformation`, failure → `LogError` with `RequestName`, `TraceId`, `JourneyId`, and the full `AxisErrorList`).
4. **Return** the result (or yield items for streams).

> Pipelines are **per request type**. `IAxisPipelineBehavior<CreateOrderCommand>` is its own type — registering an open generic (`IAxisPipelineBehavior<>`) gives you all of them at once.

---

## Real-world examples

### 1. Command from a controller

```csharp
public class OrdersController(IAxisMediator mediator) : ControllerBase
{
    [HttpPost]
    public Task<AxisResult<CreateOrderResponse>> CreateAsync(CreateOrderCommand cmd)
        => mediator.Cqrs.ExecuteAsync<CreateOrderCommand, CreateOrderResponse>(cmd);
}
```

**Why it pays off:** the controller is a one-line forward to the dispatcher; the pipeline (validation, logging, telemetry, performance, your custom behaviours) wraps the call automatically.

### 2. Query in a background job

```csharp
public class NightlyReportJob(IAxisMediator mediator)
{
    public async Task RunAsync(AxisEntityId tenantId)
    {
        var report = await mediator.Cqrs.QueryAsync<NightlyReportQuery, NightlyReportResponse>(
            new(tenantId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1))));
        // …
    }
}
```

**Why it pays off:** queries run through the same pipeline as HTTP requests — same logging, same telemetry. The job stays small.

### 3. Stream over an export

```csharp
await foreach (var row in mediator.Cqrs.StreamAsync<ExportPeopleQuery, PersonRow>(query))
    await writer.WriteAsync(row);
```

**Why it pays off:** the export uses one `IAsyncEnumerable<PersonRow>` from end to end; the producer can stream from the database without buffering, the consumer writes one row at a time.

### 4. Dispatching from another handler (sparingly)

```csharp
public class CreateOrderHandler(IAxisMediator mediator, ...)
    : IAxisCommandHandler<CreateOrderCommand, CreateOrderResponse>
{
    public Task<AxisResult<CreateOrderResponse>> HandleAsync(CreateOrderCommand cmd)
        => mediator.Cqrs.QueryAsync<GetCustomerQuery, GetCustomerResponse>(new(cmd.CustomerId))
            .ThenAsync(customer => /* … */);
}
```

**Why it pays off:** sometimes the cleanest way to read a customer is to use the same query the API uses. The dispatcher logs and traces the inner call too — a "free" sub-span.

> Use it sparingly. A handler that calls many other handlers is often a saga in disguise.

---

## See also

- [CQRS · commands, queries, streams, events](cqrs.md) — the request shapes
- [Pipeline behaviours](pipeline-behaviors.md) — what wraps every dispatch
- [Pipeline context](pipeline-context.md) — share values between behaviours
- [Registration & scanning](registration.md) — how handlers get into DI

---

↩ [Back to AxisMediator docs](README.md)
