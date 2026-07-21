# API reference

> The complete catalogue, grouped by responsibility. Use it for lookup — each group links back to its detail page.

---

## Mediator — `IAxisMediator`

| Member | Signature | Description |
|---|---|---|
| `CancellationToken` | `CancellationToken` | the ambient token from `IAxisMediatorContextAccessor` |
| `TraceId` | `string` | captured at construction: `Activity.Current?.TraceId.ToString()` or a fresh `Guid` |
| `OriginId` | `string?` | upstream system id |
| `JourneyId` | `string?` | saga / long-running journey id |
| `AxisEntityId` | `AxisEntityId?` | active identity |
| `Cqrs` | `IAxisMediatorHandler` | the dispatcher |

→ [The mediator and the accessors](mediator-and-accessors.md)

---

## Accessors

| Type | Lifetime | Description |
|---|---|---|
| `IAxisMediatorAccessor` | singleton | `IAxisMediator? AxisMediator { get; set; }` — last constructed |
| `IAxisMediatorContextAccessor` | singleton | `OriginId`/`JourneyId`/`AxisEntityId`/`CancellationToken` — `AsyncLocal`-backed |
| `IAxisMediatorContextAccessor.IsAuthenticated` | computed | `AxisEntityId != null` |

→ [The mediator and the accessors](mediator-and-accessors.md)

---

## Dispatcher — `IAxisMediatorHandler`

| Method | Signature | Description |
|---|---|---|
| `ExecuteAsync<TCommand>` | `Task<AxisResult> ExecuteAsync<TCommand>(TCommand command) where TCommand : IAxisCommand` | dispatch void-command |
| `ExecuteAsync<TCommand, TResponse>` | `Task<AxisResult<TResponse>> ExecuteAsync<TCommand, TResponse>(TCommand command) where TCommand : IAxisCommand<TResponse> where TResponse : IAxisCommandResponse` | dispatch typed-command |
| `QueryAsync<TQuery, TResponse>` | `Task<AxisResult<TResponse>> QueryAsync<TQuery, TResponse>(TQuery query) where TQuery : IAxisQuery<TResponse> where TResponse : IAxisQueryResponse` | dispatch query |
| `StreamAsync<TQuery, TItem>` | `IAsyncEnumerable<TItem> StreamAsync<TQuery, TItem>(TQuery query) where TQuery : IAxisStreamQuery<TItem>` | dispatch stream-query |

Missing handler → `AxisError.NotFound($"HANDLER_NOT_FOUND_{typeof(TRequest).Name}")` (or `InvalidOperationException` for streams).

→ [Dispatching · `IAxisMediatorHandler`](dispatching.md)

---

## CQRS contracts

| Type | Where | Description |
|---|---|---|
| `IAxisRequest` | `AxisMediator.Contracts.CQRS` | marker for "this is a mediator request" |
| `IAxisResponse` | `AxisMediator.Contracts.CQRS` | marker for "this is a mediator response" |
| `IAxisCommand` | `Commands` | void-command marker |
| `IAxisCommand<TResponse>` | `Commands` | typed-command marker |
| `IAxisCommandResponse` | `Commands` | response marker |
| `IAxisCommandHandler<TCommand>` | `Commands` | `Task<AxisResult> HandleAsync(TCommand)` |
| `IAxisCommandHandler<TCommand, TResponse>` | `Commands` | `Task<AxisResult<TResponse>> HandleAsync(TCommand)` |
| `IAxisQuery` | `Queries` | base query marker |
| `IAxisQuery<TResponse>` | `Queries` | typed-query marker |
| `IAxisQueryResponse` | `Queries` | response marker |
| `IAxisQueryHandler<TQuery, TResponse>` | `Queries` | `Task<AxisResult<TResponse>> HandleAsync(TQuery)` |
| `IAxisStreamQuery<TItem>` | `Queries` | stream-query marker |
| `IAxisStreamQueryHandler<TQuery, TItem>` | `Queries` | `IAsyncEnumerable<TItem> HandleAsync(TQuery)` |
| `IAxisEvent` | `Events` | event marker with optional `OrderingKey` (outbox FIFO partition key) |
| `IAxisEventHandler<TEvent>` | `Events` | `Task<AxisResult> HandleAsync(TEvent)` |

→ [CQRS · commands, queries, streams, events](cqrs.md)

---

## Pipeline

| Type | Description |
|---|---|
| `IAxisPipelineBehavior<TRequest>` | open-generic behaviour for void-command requests |
| `IAxisPipelineBehavior<TRequest, TResponse>` | open-generic behaviour for typed-response requests |
| `AxisPipelineContext` | per-call dictionary, `Items` + `Get<T>(key)` + `Set<T>(key, value)` |
| `AxisPipelineContextKeys.Span` | constant `"axis.pipeline.span"` — the `IAxisSpan` set by `TelemetryBehavior` |

→ [Pipeline behaviours](pipeline-behaviors.md) · [Pipeline context](pipeline-context.md)

---

## In-box behaviour — `PerformanceBehavior<TRequest, TResponse>`

| Aspect | Value |
|---|---|
| Threshold | `500 ms` |
| Emits | `IAxisLogger<TRequest>.LogWarning($"Slow request: {…} took {…}ms")` |
| Wires only | typed-response requests (`<TRequest, TResponse>`) |

→ [Performance behaviour](performance-behavior.md)

---

## DI extensions

| Extension | Effect |
|---|---|
| `services.AddAxisMediator()` | registers `IAxisMediatorHandler`/`IAxisMediator` (scoped) + accessors (singleton) |
| `services.AddPerformanceBehavior()` | registers `PerformanceBehavior<,>` as transient `IAxisPipelineBehavior<,>` |
| `services.AddCqrsMediator(Assembly)` | scans the assembly for handlers, registers each as transient against its interface |

→ [Registration & scanning](registration.md)

---

## See also

- [Getting started](getting-started.md) — install and dispatch
- [Why AxisMediator?](why-axismediator.md) — the case for the abstraction
- [Full documentation](README.md) — the map of the whole documentation

---

↩ [Back to AxisMediator docs](README.md)
