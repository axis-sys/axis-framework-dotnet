# AxisMediator — Documentação

> 🌐 [English (README principal)](../../../en-us/2-ApplicationFlow/AxisMediator/README.md)

**Um mediator CQRS in-process construído sobre `AxisResult`** — commands, queries e stream queries tipados; um pipeline tipado (`IAxisPipelineBehavior`) com um `AxisPipelineContext` compartilhado; um `IAxisMediator` ambiente carregando `TraceId`/`OriginId`/`JourneyId`/`AxisEntityId`/`CancellationToken`; eventos fluem pelo `AxisBus`; behaviours de observabilidade plugam de `AxisLogger`, `AxisValidator`, `AxisTelemetry`.

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

// na borda
var result = await mediator.Cqrs.ExecuteAsync<CreateOrderCommand, CreateOrderResponse>(cmd);
```

Use esta página como **mapa**: leia o tronco abaixo (~5 min) e salte direto para o detalhe do grupo que você precisa — sem ler centenas de linhas.

---

## O tronco (leia primeiro)

### CQRS em 60 segundos

| Conceito | Marker interface | Handler | O que roda |
|---|---|---|---|
| **Command** sem response | `IAxisCommand` | `IAxisCommandHandler<TCommand>` | `Task<AxisResult>` |
| **Command** com response tipada | `IAxisCommand<TResponse>` (`TResponse : IAxisCommandResponse`) | `IAxisCommandHandler<TCommand, TResponse>` | `Task<AxisResult<TResponse>>` |
| **Query** com response tipada | `IAxisQuery<TResponse>` (`TResponse : IAxisQueryResponse`) | `IAxisQueryHandler<TQuery, TResponse>` | `Task<AxisResult<TResponse>>` |
| **Stream query** com item tipado | `IAxisStreamQuery<TItem>` | `IAxisStreamQueryHandler<TQuery, TItem>` | `IAsyncEnumerable<TItem>` |
| **Evento** (fan-out, fire-and-forget) | `IAxisEvent` | `IAxisEventHandler<TEvent>` | `Task<AxisResult>` (por handler) |

→ **[CQRS — commands, queries, streams, eventos](cqrs.md)**

### A superfície do mediator

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

`IAxisMediator` é o **contexto ambiente** de uma request. Suas propriedades vêm de `IAxisMediatorContextAccessor` (backed por `AsyncLocal`). A propriedade `Cqrs` despacha commands/queries/streams. → **[O mediator e os accessors](mediator-and-accessors.md)**

### `IAxisMediatorHandler` — o dispatcher

```csharp
public interface IAxisMediatorHandler
{
    Task<AxisResult>             ExecuteAsync<TCommand>(TCommand command);
    Task<AxisResult<TResponse>>  ExecuteAsync<TCommand, TResponse>(TCommand command);
    Task<AxisResult<TResponse>>  QueryAsync<TQuery, TResponse>(TQuery query);
    IAsyncEnumerable<TItem>      StreamAsync<TQuery, TItem>(TQuery query);
}
```

Quatro métodos, um único trabalho: resolver o handler, construir o pipeline, rodar. Um handler ausente retorna `AxisError.NotFound("HANDLER_NOT_FOUND_X")`. → **[Despachando · `IAxisMediatorHandler`](dispatching.md)**

### Pipelines — `IAxisPipelineBehavior`

O mediator embrulha cada request num pipeline de `IAxisPipelineBehavior`s (registrados em DI como open-generics). Behaviours rodam **de fora para dentro**: o primeiro registrado é o wrapper mais externo; `next()` chama o de dentro; o handler é o mais interno. → **[Pipeline behaviours](pipeline-behaviors.md)** · **[Pipeline context](pipeline-context.md)**

### Behaviour embarcado — `PerformanceBehavior`

`IAxisPipelineBehavior<TRequest, TResponse>` opt-in que alerta quando um request passa de 500 ms via `IAxisLogger<TRequest>`. → **[`PerformanceBehavior`](performance-behavior.md)**

### Instalação

```
dotnet add package AxisMediator              # o mediator + accessors + dispatcher
dotnet add package AxisMediator.Contracts    # marker interfaces (raramente adicionada sozinha)
```

O scanner CQRS vive em `AxisMediator.DependencyInjection.AddCqrsMediator(assembly)`.

→ Guia completo: **[Primeiros passos](getting-started.md)**

---

## O mapa (salte para o que precisa)

| Grupo | Você quer… | Detalhe |
|---|---|---|
| **CQRS** | modelar commands, queries, streams, eventos | [cqrs.md](cqrs.md) |
| **Mediator · `IAxisMediator`** ⭐ | o contexto ambiente que cada handler lê | [mediator-and-accessors.md](mediator-and-accessors.md) |
| **Dispatcher · `IAxisMediatorHandler`** | os quatro `ExecuteAsync`/`QueryAsync`/`StreamAsync` | [dispatching.md](dispatching.md) |
| **Pipelines · `IAxisPipelineBehavior`** | escrever um behaviour transversal | [pipeline-behaviors.md](pipeline-behaviors.md) |
| **Pipeline context** | passar valores entre behaviours | [pipeline-context.md](pipeline-context.md) |
| **Performance behaviour** | o alerta de slow-request da caixa | [performance-behavior.md](performance-behavior.md) |
| **Registro e scanning** | `AddAxisMediator` + `AddCqrsMediator(assembly)` | [registration.md](registration.md) |
| **Por quê?** | o argumento contra MediatR | [why-axismediator.md](why-axismediator.md) |
| **Referência** | cada membro num só lugar | [api-reference.md](api-reference.md) |

**Comece aqui:** [Primeiros passos](getting-started.md) · [CQRS](cqrs.md) · [O mediator e os accessors](mediator-and-accessors.md)

**Fundamentos:** [Despachando · `IAxisMediatorHandler`](dispatching.md) · [Pipeline behaviours](pipeline-behaviors.md) · [Pipeline context](pipeline-context.md)

**Referência e extras:** [Performance behaviour](performance-behavior.md) · [Registro e scanning](registration.md) · [Por que AxisMediator?](why-axismediator.md) · [Referência da API](api-reference.md)

---

## Princípios de design

1. **CQRS está no sistema de tipos.** `IAxisCommand`, `IAxisQuery`, `IAxisEvent` dizem o que são. Handlers não podem ser confundidos uns com outros.
2. **Erros são valores.** Todo dispatch retorna `AxisResult` — até "handler não encontrado" é um `NotFound` tipado.
3. **Contexto ambiente vive em `AsyncLocal`.** `TraceId`/`OriginId`/`JourneyId`/`AxisEntityId`/`CancellationToken` viajam sem threading de parâmetro.
4. **Pipelines são open-generic.** Behaviours registram contra `IAxisPipelineBehavior<>` / `<,>` — um registro cobre cada tipo de request.
5. **Sem "o mediator faz tudo".** Publicar evento é `AxisBus`. Validar é `AxisValidator`. Traçar é `AxisTelemetry`. O mediator despacha.

---

## Licença

Apache 2.0
