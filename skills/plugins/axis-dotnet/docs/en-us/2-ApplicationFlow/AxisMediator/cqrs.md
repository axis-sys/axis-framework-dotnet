# CQRS · commands, queries, streams, events

> Five request shapes — modelled as marker interfaces in `AxisMediator.Contracts.CQRS.*`. Every shape has a typed handler interface and a corresponding dispatch method.

```csharp
// command without response
public record DeletePersonCommand(AxisEntityId PersonId) : IAxisCommand;

// command with response
public record CreateOrderCommand(...) : IAxisCommand<CreateOrderResponse>;
public record CreateOrderResponse(AxisEntityId OrderId) : IAxisCommandResponse;

// query
public record GetPersonQuery(AxisEntityId PersonId) : IAxisQuery<GetPersonResponse>;
public record GetPersonResponse(AxisEntityId PersonId, string DisplayName) : IAxisQueryResponse;

// stream query
public record StreamLogsQuery(AxisEntityId TenantId) : IAxisStreamQuery<LogLine>;

// event
public record OrderCreatedEvent(AxisEntityId OrderId) : IAxisEvent;
```

---

## When to use which

| Shape | Intent | Example |
|---|---|---|
| `IAxisCommand` | change state; no response | "delete this person" |
| `IAxisCommand<TResponse>` | change state; return data (an id, a token) | "create an order, return the id" |
| `IAxisQuery<TResponse>` | read state; return data | "fetch this person" |
| `IAxisStreamQuery<TItem>` | read state; return many items lazily | "stream every log line for tenant X" |
| `IAxisEvent` | something happened; many handlers may react | "order created" |

## When *not* to use

| You want to… | Use instead |
|---|---|
| send a long-running, multi-step workflow | a [`Saga`](../AxisSaga/README.md) |
| invoke a remote service | a port (your own interface) + adapter, called from a handler |
| chain side effects off a command | publish an event on the [`Bus`](../AxisBus/README.md) and let handlers react |

---

## The contracts

### Commands

```csharp
public interface IAxisRequest;
public interface IAxisResponse;

public interface IAxisCommand : IAxisRequest;
public interface IAxisCommand<TResponse> : IAxisRequest where TResponse : IAxisCommandResponse;
public interface IAxisCommandResponse : IAxisResponse;

public interface IAxisCommandHandler<in TCommand> where TCommand : IAxisCommand
{
    Task<AxisResult> HandleAsync(TCommand command);
}

public interface IAxisCommandHandler<in TCommand, TResponse>
    where TCommand : IAxisCommand<TResponse>
    where TResponse : IAxisCommandResponse
{
    Task<AxisResult<TResponse>> HandleAsync(TCommand command);
}
```

### Queries

```csharp
public interface IAxisQuery : IAxisRequest;
public interface IAxisQuery<TResponse> : IAxisQuery where TResponse : IAxisQueryResponse;
public interface IAxisQueryResponse : IAxisResponse;

public interface IAxisQueryHandler<in TQuery, TResponse>
    where TQuery : IAxisQuery<TResponse>
    where TResponse : IAxisQueryResponse
{
    Task<AxisResult<TResponse>> HandleAsync(TQuery query);
}
```

### Stream queries

```csharp
public interface IAxisStreamQuery<out TItem> : IAxisRequest;

public interface IAxisStreamQueryHandler<in TQuery, out TItem>
    where TQuery : IAxisStreamQuery<TItem>
{
    IAsyncEnumerable<TItem> HandleAsync(TQuery query);
}
```

> Streams **do not** return `AxisResult` — they are an `IAsyncEnumerable<TItem>`. Errors mid-stream throw; the consumer handles them like any `await foreach`.

### Events

```csharp
public interface IAxisEvent
{
    string? OrderingKey => null;
}

public interface IAxisEventHandler<in TEvent> where TEvent : IAxisEvent
{
    Task<AxisResult> HandleAsync(TEvent @event);
}
```

`OrderingKey` is an optional partition/ordering key used by the outbox to deliver events sharing the same key in FIFO order; when left `null`, it falls back to the ambient `JourneyId`, then to the event's own id.

Events are published via [`AxisBus`](../AxisBus/README.md), **not** via `IAxisMediator`. The mediator dispatches commands, queries and streams; the bus broadcasts events.

---

## Real-world examples

### 1. Command with response, query, event — typical flow

```csharp
// commands and queries
public record CreateOrderCommand(...) : IAxisCommand<CreateOrderResponse>;
public record GetOrderQuery(AxisEntityId OrderId) : IAxisQuery<GetOrderResponse>;

// event enqueued in the same transaction as the command
public record OrderCreatedEvent(AxisEntityId OrderId) : IAxisEvent;

// command handler that publishes the event
public class CreateOrderHandler(IOrderFactory factory, IUnitOfWork uow, IAxisBus bus)
    : IAxisCommandHandler<CreateOrderCommand, CreateOrderResponse>
{
    public Task<AxisResult<CreateOrderResponse>> HandleAsync(CreateOrderCommand cmd)
        => factory.CreateAsync(cmd)
            .ThenAsync(o => bus.PublishAsync(new OrderCreatedEvent(o.OrderId)).Map(_ => o))
            .ThenAsync(o => uow.SaveChangesAsync().Map(_ => o))
            .MapAsync(o => new CreateOrderResponse(o.OrderId));
}
```

**Why it pays off:** the event is enqueued *before* the commit. With an outbox bus, `PublishAsync` writes the event on the unit of work's connection and the single `SaveChangesAsync` commits the order and the event together — atomically, or neither. Invert the two and you are back to the classic dual-write: the state commits, then the event is lost if the publish (or the process) dies.

### 2. Stream query — paginate-by-design

```csharp
public record StreamLogsQuery(AxisEntityId TenantId) : IAxisStreamQuery<LogLine>;

public class StreamLogsHandler(ILogRepo repo) : IAxisStreamQueryHandler<StreamLogsQuery, LogLine>
{
    public async IAsyncEnumerable<LogLine> HandleAsync(StreamLogsQuery q)
    {
        await foreach (var batch in repo.StreamAsync(q.TenantId).ConfigureAwait(false))
            yield return batch;
    }
}

// caller
await foreach (var line in mediator.Cqrs.StreamAsync<StreamLogsQuery, LogLine>(query))
    Console.WriteLine(line);
```

**Why it pays off:** the consumer iterates without pulling everything into memory; the producer can read from the database one batch at a time.

### 3. Event fan-out

```csharp
public class WarmCacheHandler(IAxisCache cache)      : IAxisEventHandler<OrderCreatedEvent> { /* ... */ }
public class SendEmailHandler(IAxisEmailService mail): IAxisEventHandler<OrderCreatedEvent> { /* ... */ }
```

The two handlers run concurrently when the bus publishes — the publisher does not know they exist.

---

## See also

- [Dispatching · `IAxisMediatorHandler`](dispatching.md) — the four methods that drive these
- [The mediator and the accessors](mediator-and-accessors.md) — what's in the ambient context
- [Pipeline behaviours](pipeline-behaviors.md) — cross-cutting code that wraps every dispatch
- [`AxisBus`](../AxisBus/README.md) — events are published here, not via the mediator

---

↩ [Back to AxisMediator docs](README.md)
