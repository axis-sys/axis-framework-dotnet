# API reference

> The complete contract catalog, grouped by responsibility. Use it for lookup — every type here is a pure abstraction; the runtime behaviour lives in [AxisMediator](../../2-ApplicationFlow/AxisMediator/README.md).

All types live under the `AxisMediator.Contracts` root namespace and its sub-namespaces (`CQRS`, `CQRS.Commands`, `CQRS.Queries`, `CQRS.Events`, `CQRS.Handlers`, `Pipelines`).

---

## CQRS request & response markers

Marker interfaces that classify a message. They carry no members — the dispatcher routes by type.

| Type | Description |
|------|-------------|
| `IAxisRequest` | base marker for anything dispatchable (commands, queries, stream queries) |
| `IAxisResponse` | base marker for any response payload |
| `IAxisCommand : IAxisRequest` | a state-changing command with no response |
| `IAxisCommand<TResponse> : IAxisRequest` | a command returning `TResponse`, where `TResponse : IAxisCommandResponse` |
| `IAxisCommandResponse : IAxisResponse` | marker for a command's response payload |
| `IAxisQuery : IAxisRequest` | base marker for a read query |
| `IAxisQuery<TResponse> : IAxisQuery` | a query returning `TResponse`, where `TResponse : IAxisQueryResponse` |
| `IAxisQueryResponse : IAxisResponse` | marker for a query's response payload |
| `IAxisStreamQuery<out TItem> : IAxisRequest` | a query that streams a sequence of `TItem` |
| `IAxisEvent` | marker for a fact that has already happened |

---

## Handlers

One handler interface per request kind. Each returns `AxisResult` / `AxisResult<TResponse>` (from `Axis`), except the stream handler which returns `IAsyncEnumerable<TItem>`.

| Type | Method | Signature |
|------|--------|-----------|
| `IAxisCommandHandler<in TCommand>` where `TCommand : IAxisCommand` | `HandleAsync` | `Task<AxisResult> HandleAsync(TCommand command)` |
| `IAxisCommandHandler<in TCommand, TResponse>` where `TCommand : IAxisCommand<TResponse>`, `TResponse : IAxisCommandResponse` | `HandleAsync` | `Task<AxisResult<TResponse>> HandleAsync(TCommand command)` |
| `IAxisQueryHandler<in TQuery, TResponse>` where `TQuery : IAxisQuery<TResponse>`, `TResponse : IAxisQueryResponse` | `HandleAsync` | `Task<AxisResult<TResponse>> HandleAsync(TQuery query)` |
| `IAxisEventHandler<in TEvent>` where `TEvent : IAxisEvent` | `HandleAsync` | `Task<AxisResult> HandleAsync(TEvent @event)` |
| `IAxisStreamQueryHandler<in TQuery, out TItem>` where `TQuery : IAxisStreamQuery<TItem>` | `HandleAsync` | `IAsyncEnumerable<TItem> HandleAsync(TQuery query)` |

---

## Execution facade — `IAxisMediatorHandler`

The dispatch surface exposed by `IAxisMediator.Cqrs`. Each method routes a request to its registered handler.

| Method | Signature | Description |
|--------|-----------|-------------|
| `ExecuteAsync` | `Task<AxisResult> ExecuteAsync<TCommand>(TCommand command)` where `TCommand : IAxisCommand` | dispatch a command with no response |
| `ExecuteAsync` | `Task<AxisResult<TResponse>> ExecuteAsync<TCommand, TResponse>(TCommand command)` where `TCommand : IAxisCommand<TResponse>`, `TResponse : IAxisCommandResponse` | dispatch a command returning `TResponse` |
| `QueryAsync` | `Task<AxisResult<TResponse>> QueryAsync<TQuery, TResponse>(TQuery query)` where `TQuery : IAxisQuery<TResponse>`, `TResponse : IAxisQueryResponse` | dispatch a read query |
| `StreamAsync` | `IAsyncEnumerable<TItem> StreamAsync<TQuery, TItem>(TQuery query)` where `TQuery : IAxisStreamQuery<TItem>` | dispatch a streaming query |

---

## Mediator & context

### `IAxisMediator`

The ambient request context injected into application code.

| Member | Type | Description |
|--------|------|-------------|
| `CancellationToken` | `CancellationToken` (get) | cancellation token for the current request |
| `TraceId` | `string` (get) | correlation id for the request |
| `OriginId` | `string?` (get) | id of the originating request/system, if any |
| `JourneyId` | `string?` (get) | id grouping a multi-step user journey, if any |
| `AxisEntityId` | `AxisEntityId?` (get) | the authenticated caller's identity, if any |
| `Cqrs` | `IAxisMediatorHandler` (get) | the dispatch facade for executing requests |

### `IAxisMediatorAccessor`

Ambient access slot for the current `IAxisMediator` (e.g. for async-local storage).

| Member | Type | Description |
|--------|------|-------------|
| `AxisMediator` | `IAxisMediator?` (get/set) | the mediator for the current scope |

### `IAxisMediatorContextAccessor`

Mutable seam used while building the `IAxisMediator` context (e.g. from an HTTP request).

| Member | Type | Description |
|--------|------|-------------|
| `OriginId` | `string?` (get/set) | originating id to propagate |
| `JourneyId` | `string?` (get/set) | journey id to propagate |
| `AxisEntityId` | `AxisEntityId?` (get/set) | the authenticated caller's identity |
| `CancellationToken` | `CancellationToken` (get/set) | cancellation token for the request |
| `IsAuthenticated` | `bool` (get, default impl) | `true` when `AxisEntityId != null` |

---

## Pipelines

### Behaviours

Cross-cutting steps wrapped around handler execution. Each receives the request, the shared `AxisPipelineContext`, and a `next` delegate that invokes the rest of the pipeline.

| Type | Method | Signature |
|------|--------|-----------|
| `IAxisPipelineBehavior<in TRequest>` where `TRequest : IAxisRequest` | `HandleAsync` | `Task<AxisResult> HandleAsync(TRequest request, AxisPipelineContext context, Func<Task<AxisResult>> next)` |
| `IAxisPipelineBehavior<in TRequest, TResponse>` where `TRequest : IAxisRequest`, `TResponse : IAxisResponse` | `HandleAsync` | `Task<AxisResult<TResponse>> HandleAsync(TRequest request, AxisPipelineContext context, Func<Task<AxisResult<TResponse>>> next)` |

### `AxisPipelineContext`

`sealed class` carrying state shared across behaviours for a single request execution. Items set by an upstream behaviour can be read by downstream ones through typed keys.

| Member | Signature | Description |
|--------|-----------|-------------|
| `Items` | `IDictionary<string, object?> Items { get; }` | backing store (ordinal string keys) |
| `Get<T>` | `T? Get<T>(string key)` | typed read; returns `default` when missing or of the wrong type |
| `Set<T>` | `void Set<T>(string key, T value)` | typed write |

### `AxisPipelineContextKeys`

`static class` of well-known keys written into `AxisPipelineContext` by built-in behaviours.

| Member | Signature | Description |
|--------|-----------|-------------|
| `Span` | `const string Span = "axis.pipeline.span"` | the active `IAxisSpan` for the request, set by the telemetry behaviour |

---

## See also

- [AxisMediator](../../2-ApplicationFlow/AxisMediator/README.md) — the concrete dispatcher, getting-started, pipelines and CQRS usage
- [Full documentation](README.md) — the map of this package

---

↩ [Back to AxisMediator.Contracts docs](README.md)
