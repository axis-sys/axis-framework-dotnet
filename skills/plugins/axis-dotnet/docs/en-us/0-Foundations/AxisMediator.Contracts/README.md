# AxisMediator.Contracts — Documentation

> 🌐 [Português (documentação navegável)](../../../pt-br/0-Foundations/AxisMediator.Contracts/README.md)

**The pure contracts of the Axis mediator** — CQRS markers, handler interfaces, the execution facade, the `IAxisMediator` ambient context and the pipeline abstractions. Zero infrastructure: just the abstractions every consumer shares.

```csharp
// A command and its handler depend only on the contracts — never on the implementation.
public sealed record CreateOrderCommand(Guid CustomerId, Guid ProductId, int Quantity)
    : IAxisCommand<CreateOrderResponse>;

public sealed record CreateOrderResponse(Guid OrderId) : IAxisCommandResponse;

public sealed class CreateOrderHandler(IAxisMediator mediator)
    : IAxisCommandHandler<CreateOrderCommand, CreateOrderResponse>
{
    public Task<AxisResult<CreateOrderResponse>> HandleAsync(CreateOrderCommand command)
        => /* ... */;
}
```

Use this page as a **map**: read the trunk below (~5 min), then jump to the [API reference](api-reference.md) for the full catalog — or to **[AxisMediator](../../2-ApplicationFlow/AxisMediator/README.md)** for the usage guide, getting-started, pipelines and CQRS walkthroughs.

---

## The trunk (read first)

### Why a contracts-only package?

`AxisMediator.Contracts` holds the **abstractions** of the mediator and nothing else — no DI wiring, no dispatcher, no behaviours. Because it is pure types, it lives in `0-Foundations`, while the concrete **[implementation](../../2-ApplicationFlow/AxisMediator/README.md)** (`AxisMediator`) lives in `2-ApplicationFlow`.

This split lets packages such as **AxisBus**, **AxisSaga**, **AxisValidator**, **AxisLogger** and **AxisTelemetry** depend only on the contracts — declaring commands, handlers or pipeline behaviours — without pulling in the dispatcher itself.

### CQRS in one minute

Every message is a **request** that implements a marker, and every request has a matching **handler**:

- **Command** — changes state. `IAxisCommand` (no response) or `IAxisCommand<TResponse>`. Handled by `IAxisCommandHandler<TCommand>` / `IAxisCommandHandler<TCommand, TResponse>`.
- **Query** — reads state. `IAxisQuery<TResponse>`, handled by `IAxisQueryHandler<TQuery, TResponse>`. Streaming reads use `IAxisStreamQuery<TItem>` + `IAxisStreamQueryHandler<TQuery, TItem>`.
- **Event** — a fact that already happened. `IAxisEvent`, handled by `IAxisEventHandler<TEvent>`.

Handlers return `Task<AxisResult>` or `Task<AxisResult<TResponse>>` — failures are values, not exceptions.

### Executing requests

`IAxisMediator` is the ambient context injected into your code. It carries correlation (`TraceId`, `OriginId`, `JourneyId`), the caller's `AxisEntityId`, the request `CancellationToken`, and the `Cqrs` facade — `IAxisMediatorHandler` — used to dispatch:

```csharp
AxisResult<CreateOrderResponse> result =
    await mediator.Cqrs.ExecuteAsync<CreateOrderCommand, CreateOrderResponse>(command);
```

→ Full catalog: **[API reference](api-reference.md)**

### Pipeline behaviours

Cross-cutting steps (logging, telemetry, validation) implement `IAxisPipelineBehavior<TRequest>` or `IAxisPipelineBehavior<TRequest, TResponse>`. They share state across one execution through `AxisPipelineContext` (`Items` / `Get` / `Set`), keyed by the well-known constants in `AxisPipelineContextKeys`.

### Installation

```
dotnet add package AxisMediator.Contracts
```

> Most apps install **AxisMediator** instead, which references these contracts transitively. Add this package directly only when you author abstractions (e.g. a library declaring handlers or behaviours) that must not depend on the implementation.

---

## The map (jump to what you need)

| Group | You want to… | Detail |
|---|---|---|
| **Reference · all contracts** ⭐ | look up every marker, handler, facade and pipeline type | [api-reference.md](api-reference.md) |
| **Usage · `AxisMediator`** | getting-started, pipelines, CQRS and the dispatcher implementation | [AxisMediator](../../2-ApplicationFlow/AxisMediator/README.md) |

**Start here:** [API reference](api-reference.md) · [AxisMediator usage guide](../../2-ApplicationFlow/AxisMediator/README.md)

---

## Design principles

1. **Abstractions in Foundations, implementation in ApplicationFlow.** The contracts have zero dependencies on the dispatcher, so consumers can target them in isolation.
2. **Errors are values, not exceptions.** Every handler returns `AxisResult` / `AxisResult<TResponse>`; failure is part of the signature.
3. **The type system is the contract.** Marker interfaces and generic constraints make illegal request/handler pairings unrepresentable.
4. **One marker per intent.** Commands, queries, streams and events each have their own marker, so the dispatcher routes by type.
5. **Cross-cutting state stays explicit.** Behaviours pass data through a typed `AxisPipelineContext`, never through hidden globals.

---

## License

Apache 2.0
