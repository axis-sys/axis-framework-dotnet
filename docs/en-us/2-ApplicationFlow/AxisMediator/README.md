# AxisMediator — Documentation

> 🌐 [Português (documentação navegável)](../../../pt-br/2-ApplicationFlow/AxisMediator/README.md)

**An in-process CQRS mediator built on `AxisResult`** — typed commands, queries and streaming queries; a typed pipeline (`IAxisPipelineBehavior`) with a shared `AxisPipelineContext`; an ambient `IAxisMediator` carrying `TraceId`/`OriginId`/`JourneyId`/`AxisEntityId`/`CancellationToken`; events flow through `AxisBus`; observability behaviours plug in from `AxisLogger`, `AxisValidator`, `AxisTelemetry`.

```csharp
public record CreateOrderCommand(AxisEntityId CustomerId, AxisEntityId ProductId, int Quantity)
    : IAxisCommand<CreateOrderResponse>;

public record CreateOrderResponse(AxisEntityId OrderId) : IAxisCommandResponse;

public class CreateOrderHandler(IOrderFactory factory, IUnitOfWork uow)
    : IAxisCommandHandler<CreateOrderCommand, CreateOrderResponse>
{
    public Task<AxisResult<CreateOrderResponse>> HandleAsync(CreateOrderCommand cmd)
        => factory.CreateAsync(cmd)
            .ThenAsync(order => uow.SaveChangesAsync().Map(_ => order))
            .MapAsync(order => new CreateOrderResponse(order.OrderId));
}

// at the edge
var result = await mediator.Cqrs.ExecuteAsync<CreateOrderCommand, CreateOrderResponse>(cmd);
```

Use this page as a **map**: read the trunk below (~5 min) and jump straight to the detail of the group you need — without reading hundreds of lines.

---

## The trunk (read first)

### CQRS in 60 seconds

| Concept | Marker interface | Handler | What runs |
|---|---|---|---|
| **Command** with no response | `IAxisCommand` | `IAxisCommandHandler<TCommand>` | `Task<AxisResult>` |
| **Command** with a typed response | `IAxisCommand<TResponse>` (`TResponse : IAxisCommandResponse`) | `IAxisCommandHandler<TCommand, TResponse>` | `Task<AxisResult<TResponse>>` |
| **Query** with a typed response | `IAxisQuery<TResponse>` (`TResponse : IAxisQueryResponse`) | `IAxisQueryHandler<TQuery, TResponse>` | `Task<AxisResult<TResponse>>` |
| **Stream query** with a typed item | `IAxisStreamQuery<TItem>` | `IAxisStreamQueryHandler<TQuery, TItem>` | `IAsyncEnumerable<TItem>` |
| **Event** (fan-out, fire-and-forget) | `IAxisEvent` | `IAxisEventHandler<TEvent>` | `Task<AxisResult>` (per handler) |

→ **[CQRS — commands, queries, streams, events](cqrs.md)**

### The mediator surface

```csharp
public interface IAxisMediator
{
    CancellationToken CancellationToken { get; }
    string TraceId { get; }
    string? OriginId { get; }
    string? JourneyId { get; }
    AxisEntityId? AxisEntityId { get; }
    IAxisMediatorHandler Cqrs { get; }
}
```

`IAxisMediator` is the **ambient context** for a request. Its properties come from `IAxisMediatorContextAccessor` (`AsyncLocal`-backed). The `Cqrs` property dispatches commands/queries/streams. → **[The mediator and the accessors](mediator-and-accessors.md)**

### `IAxisMediatorHandler` — the dispatcher

```csharp
public interface IAxisMediatorHandler
{
    Task<AxisResult>             ExecuteAsync<TCommand>(TCommand command);
    Task<AxisResult<TResponse>>  ExecuteAsync<TCommand, TResponse>(TCommand command);
    Task<AxisResult<TResponse>>  QueryAsync<TQuery, TResponse>(TQuery query);
    IAsyncEnumerable<TItem>      StreamAsync<TQuery, TItem>(TQuery query);
}
```

Four methods, one job: resolve the handler, build the pipeline, run it. A missing handler returns `AxisError.NotFound("HANDLER_NOT_FOUND_X")`. → **[Dispatching · `IAxisMediatorHandler`](dispatching.md)**

### Pipelines — `IAxisPipelineBehavior`

The mediator wraps every request in a pipeline of `IAxisPipelineBehavior`s (registered in DI as open-generics). Behaviours run **outside-in**: the first registered behaviour is the outermost wrapper; `next()` calls the inner one; the handler is innermost. → **[Pipeline behaviours](pipeline-behaviors.md)** · **[Pipeline context](pipeline-context.md)**

### Built-in behaviour — `PerformanceBehavior`

Opt-in `IAxisPipelineBehavior<TRequest, TResponse>` that warns when a request exceeds 500ms via `IAxisLogger<TRequest>`. → **[`PerformanceBehavior`](performance-behavior.md)**

### Installation

```
dotnet add package AxisMediator              # the mediator + accessors + dispatcher
dotnet add package AxisMediator.Contracts    # marker interfaces (rarely added alone)
```

The CQRS scanner lives in `AxisMediator.DependencyInjection.AddCqrsMediator(assembly)`.

→ Full guide: **[Getting started](getting-started.md)**

---

## The map (jump to what you need)

| Group | You want to… | Detail |
|---|---|---|
| **CQRS** | model commands, queries, streams, events | [cqrs.md](cqrs.md) |
| **Mediator · `IAxisMediator`** ⭐ | the ambient context every handler reads | [mediator-and-accessors.md](mediator-and-accessors.md) |
| **Dispatcher · `IAxisMediatorHandler`** | the four `ExecuteAsync`/`QueryAsync`/`StreamAsync` | [dispatching.md](dispatching.md) |
| **Pipelines · `IAxisPipelineBehavior`** | write a cross-cutting behaviour | [pipeline-behaviors.md](pipeline-behaviors.md) |
| **Pipeline context** | pass values between behaviours | [pipeline-context.md](pipeline-context.md) |
| **Performance behaviour** | the in-box slow-request warning | [performance-behavior.md](performance-behavior.md) |
| **Registration & scanning** | `AddAxisMediator` + `AddCqrsMediator(assembly)` | [registration.md](registration.md) |
| **Why?** | the case against MediatR | [why-axismediator.md](why-axismediator.md) |
| **Reference** | every member at a glance | [api-reference.md](api-reference.md) |

**Start here:** [Getting started](getting-started.md) · [CQRS](cqrs.md) · [The mediator and the accessors](mediator-and-accessors.md)

**Fundamentals:** [Dispatching · `IAxisMediatorHandler`](dispatching.md) · [Pipeline behaviours](pipeline-behaviors.md) · [Pipeline context](pipeline-context.md)

**Reference & extras:** [Performance behaviour](performance-behavior.md) · [Registration & scanning](registration.md) · [Why AxisMediator?](why-axismediator.md) · [API reference](api-reference.md)

---

## Design principles

1. **CQRS is in the type system.** `IAxisCommand`, `IAxisQuery`, `IAxisEvent` say what they are. Handlers cannot be confused for each other.
2. **Errors are values.** Every dispatch returns `AxisResult` — even "handler not found" is a typed `NotFound`.
3. **Ambient context lives in `AsyncLocal`.** `TraceId`/`OriginId`/`JourneyId`/`AxisEntityId`/`CancellationToken` travel without parameter threading.
4. **Pipelines are open-generic.** Behaviours register against `IAxisPipelineBehavior<>` / `<,>` — one registration covers every request type.
5. **No "the mediator does everything".** Sending an event is `AxisBus`. Validating is `AxisValidator`. Tracing is `AxisTelemetry`. The mediator dispatches.

---

## License

Apache 2.0
