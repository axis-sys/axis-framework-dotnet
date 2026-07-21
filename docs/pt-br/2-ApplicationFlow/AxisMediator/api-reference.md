# Referência da API

> O catálogo completo, agrupado por responsabilidade. Use para consulta — cada grupo linka de volta à sua página de detalhe.

---

## Mediator — `IAxisMediator`

| Membro | Assinatura | Descrição |
|---|---|---|
| `CancellationToken` | `CancellationToken` | o token ambiente de `IAxisMediatorContextAccessor` |
| `TraceId` | `string` | capturado na construção: `Activity.Current?.TraceId.ToString()` ou um `Guid` fresco |
| `OriginId` | `string?` | id do sistema upstream |
| `JourneyId` | `string?` | id de saga / jornada longa |
| `AxisEntityId` | `AxisEntityId?` | identidade ativa |
| `Cqrs` | `IAxisMediatorHandler` | o dispatcher |

→ [O mediator e os accessors](mediator-and-accessors.md)

---

## Accessors

| Tipo | Lifetime | Descrição |
|---|---|---|
| `IAxisMediatorAccessor` | singleton | `IAxisMediator? AxisMediator { get; set; }` — último construído |
| `IAxisMediatorContextAccessor` | singleton | `OriginId`/`JourneyId`/`AxisEntityId`/`CancellationToken` — backed por `AsyncLocal` |
| `IAxisMediatorContextAccessor.IsAuthenticated` | computado | `AxisEntityId != null` |

→ [O mediator e os accessors](mediator-and-accessors.md)

---

## Dispatcher — `IAxisMediatorHandler`

| Método | Assinatura | Descrição |
|---|---|---|
| `ExecuteAsync<TCommand>` | `Task<AxisResult> ExecuteAsync<TCommand>(TCommand command) where TCommand : IAxisCommand` | despacha void-command |
| `ExecuteAsync<TCommand, TResponse>` | `Task<AxisResult<TResponse>> ExecuteAsync<TCommand, TResponse>(TCommand command) where TCommand : IAxisCommand<TResponse> where TResponse : IAxisCommandResponse` | despacha typed-command |
| `QueryAsync<TQuery, TResponse>` | `Task<AxisResult<TResponse>> QueryAsync<TQuery, TResponse>(TQuery query) where TQuery : IAxisQuery<TResponse> where TResponse : IAxisQueryResponse` | despacha query |
| `StreamAsync<TQuery, TItem>` | `IAsyncEnumerable<TItem> StreamAsync<TQuery, TItem>(TQuery query) where TQuery : IAxisStreamQuery<TItem>` | despacha stream-query |

Handler ausente → `AxisError.NotFound($"HANDLER_NOT_FOUND_{typeof(TRequest).Name}")` (ou `InvalidOperationException` para streams).

→ [Despachando · `IAxisMediatorHandler`](dispatching.md)

---

## Contratos CQRS

| Tipo | Onde | Descrição |
|---|---|---|
| `IAxisRequest` | `AxisMediator.Contracts.CQRS` | marker "isto é um request de mediator" |
| `IAxisResponse` | `AxisMediator.Contracts.CQRS` | marker "isto é uma response de mediator" |
| `IAxisCommand` | `Commands` | marker de void-command |
| `IAxisCommand<TResponse>` | `Commands` | marker de typed-command |
| `IAxisCommandResponse` | `Commands` | marker de response |
| `IAxisCommandHandler<TCommand>` | `Commands` | `Task<AxisResult> HandleAsync(TCommand)` |
| `IAxisCommandHandler<TCommand, TResponse>` | `Commands` | `Task<AxisResult<TResponse>> HandleAsync(TCommand)` |
| `IAxisQuery` | `Queries` | marker base de query |
| `IAxisQuery<TResponse>` | `Queries` | marker de typed-query |
| `IAxisQueryResponse` | `Queries` | marker de response |
| `IAxisQueryHandler<TQuery, TResponse>` | `Queries` | `Task<AxisResult<TResponse>> HandleAsync(TQuery)` |
| `IAxisStreamQuery<TItem>` | `Queries` | marker de stream-query |
| `IAxisStreamQueryHandler<TQuery, TItem>` | `Queries` | `IAsyncEnumerable<TItem> HandleAsync(TQuery)` |
| `IAxisEvent` | `Events` | marker de evento com `OrderingKey` opcional (chave de partição FIFO do outbox) |
| `IAxisEventHandler<TEvent>` | `Events` | `Task<AxisResult> HandleAsync(TEvent)` |

→ [CQRS · commands, queries, streams, eventos](cqrs.md)

---

## Pipeline

| Tipo | Descrição |
|---|---|
| `IAxisPipelineBehavior<TRequest>` | behaviour open-generic para requests void-command |
| `IAxisPipelineBehavior<TRequest, TResponse>` | behaviour open-generic para requests com response tipada |
| `AxisPipelineContext` | dicionário por chamada, `Items` + `Get<T>(key)` + `Set<T>(key, value)` |
| `AxisPipelineContextKeys.Span` | constante `"axis.pipeline.span"` — o `IAxisSpan` setado pelo `TelemetryBehavior` |

→ [Pipeline behaviours](pipeline-behaviors.md) · [Pipeline context](pipeline-context.md)

---

## Behaviour embarcado — `PerformanceBehavior<TRequest, TResponse>`

| Aspecto | Valor |
|---|---|
| Threshold | `500 ms` |
| Emite | `IAxisLogger<TRequest>.LogWarning($"Slow request: {…} took {…}ms")` |
| Plugado só em | requests com response tipada (`<TRequest, TResponse>`) |

→ [Performance behaviour](performance-behavior.md)

---

## Extensões de DI

| Extensão | Efeito |
|---|---|
| `services.AddAxisMediator()` | registra `IAxisMediatorHandler`/`IAxisMediator` (scoped) + accessors (singleton) |
| `services.AddPerformanceBehavior()` | registra `PerformanceBehavior<,>` como transient `IAxisPipelineBehavior<,>` |
| `services.AddCqrsMediator(Assembly)` | scaneia o assembly por handlers, registra cada um como transient contra sua interface |

→ [Registro e scanning](registration.md)

---

## Veja também

- [Primeiros passos](getting-started.md) — instale e despache
- [Por que AxisMediator?](why-axismediator.md) — o argumento pela abstração
- [Documentação completa](README.md) — o mapa de toda a documentação

---

↩ [Voltar à documentação do AxisMediator](README.md)
