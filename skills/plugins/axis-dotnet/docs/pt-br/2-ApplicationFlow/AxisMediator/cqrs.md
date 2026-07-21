# CQRS · commands, queries, streams, eventos

> Cinco formatos de request — modelados como marker interfaces em `AxisMediator.Contracts.CQRS.*`. Cada formato tem uma interface de handler tipada e um método correspondente de dispatch.

```csharp
// command sem response
public record DeletePersonCommand(AxisEntityId PersonId) : IAxisCommand;

// command com response
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

## Quando usar qual

| Formato | Intenção | Exemplo |
|---|---|---|
| `IAxisCommand` | mudar estado; sem response | "delete esta pessoa" |
| `IAxisCommand<TResponse>` | mudar estado; retornar dados (um id, um token) | "crie um pedido, retorne o id" |
| `IAxisQuery<TResponse>` | ler estado; retornar dados | "busque esta pessoa" |
| `IAxisStreamQuery<TItem>` | ler estado; retornar muitos itens preguiçosamente | "stream de cada log line para o tenant X" |
| `IAxisEvent` | algo aconteceu; vários handlers podem reagir | "pedido criado" |

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| enviar um workflow longo, multi-passo | uma [`Saga`](../AxisSaga/README.md) |
| invocar um serviço remoto | uma porta (sua interface) + adapter, chamada do handler |
| encadear side effects a partir de um command | publique um evento no [`Bus`](../AxisBus/README.md) e deixe handlers reagirem |

---

## Os contratos

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

> Streams **não** retornam `AxisResult` — são um `IAsyncEnumerable<TItem>`. Erros no meio do stream lançam; o consumidor trata como qualquer `await foreach`.

### Eventos

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

`OrderingKey` é uma chave opcional de particionamento/ordenação usada pelo outbox para entregar em ordem FIFO os eventos que compartilham a mesma chave; quando deixada `null`, cai para o `JourneyId` ambiente e, depois, para o id do próprio evento.

Eventos são publicados via [`AxisBus`](../AxisBus/README.md), **não** via `IAxisMediator`. O mediator despacha commands, queries e streams; o bus broadcast eventos.

---

## Exemplos reais

### 1. Command com response, query, evento — fluxo típico

```csharp
// commands e queries
public record CreateOrderCommand(...) : IAxisCommand<CreateOrderResponse>;
public record GetOrderQuery(AxisEntityId OrderId) : IAxisQuery<GetOrderResponse>;

// evento enfileirado na mesma transação do command
public record OrderCreatedEvent(AxisEntityId OrderId) : IAxisEvent;

// command handler que publica o evento
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

**Por que compensa:** o evento é enfileirado *antes* do commit. Com um outbox bus, `PublishAsync` grava o evento na conexão do unit of work e o único `SaveChangesAsync` commita o pedido e o evento juntos — atomicamente, ou nenhum. Inverta os dois e você volta ao dual-write clássico: o estado commita, e então o evento se perde se o publish (ou o processo) morre.

### 2. Stream query — pagine por design

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

// chamador
await foreach (var line in mediator.Cqrs.StreamAsync<StreamLogsQuery, LogLine>(query))
    Console.WriteLine(line);
```

**Por que compensa:** o consumidor itera sem puxar tudo para memória; o produtor pode ler do banco um lote por vez.

### 3. Fan-out de eventos

```csharp
public class WarmCacheHandler(IAxisCache cache)      : IAxisEventHandler<OrderCreatedEvent> { /* ... */ }
public class SendEmailHandler(IAxisEmailService mail): IAxisEventHandler<OrderCreatedEvent> { /* ... */ }
```

Os dois handlers rodam concorrentemente quando o bus publica — o publicador não sabe que existem.

---

## Veja também

- [Despachando · `IAxisMediatorHandler`](dispatching.md) — os quatro métodos que dirigem isso
- [O mediator e os accessors](mediator-and-accessors.md) — o que tem no contexto ambiente
- [Pipeline behaviours](pipeline-behaviors.md) — código transversal que embrulha cada dispatch
- [`AxisBus`](../AxisBus/README.md) — eventos são publicados aqui, não via mediator

---

↩ [Voltar à documentação do AxisMediator](README.md)
