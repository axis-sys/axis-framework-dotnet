# Referência da API

> O catálogo completo de contratos, agrupado por responsabilidade. Use para consulta — cada tipo aqui é uma abstração pura; o comportamento em runtime vive em [AxisMediator](../../2-ApplicationFlow/AxisMediator/README.md).

Todos os tipos vivem sob o namespace raiz `AxisMediator.Contracts` e seus sub-namespaces (`CQRS`, `CQRS.Commands`, `CQRS.Queries`, `CQRS.Events`, `CQRS.Handlers`, `Pipelines`).

---

## Markers de request e response de CQRS

Interfaces marker que classificam uma mensagem. Elas não carregam membros — o dispatcher roteia por tipo.

| Tipo | Descrição |
|------|-------------|
| `IAxisRequest` | marker base para qualquer coisa despachável (comandos, queries, stream queries) |
| `IAxisResponse` | marker base para qualquer payload de resposta |
| `IAxisCommand : IAxisRequest` | um comando que altera estado sem resposta |
| `IAxisCommand<TResponse> : IAxisRequest` | um comando que retorna `TResponse`, onde `TResponse : IAxisCommandResponse` |
| `IAxisCommandResponse : IAxisResponse` | marker para o payload de resposta de um comando |
| `IAxisQuery : IAxisRequest` | marker base para uma query de leitura |
| `IAxisQuery<TResponse> : IAxisQuery` | uma query que retorna `TResponse`, onde `TResponse : IAxisQueryResponse` |
| `IAxisQueryResponse : IAxisResponse` | marker para o payload de resposta de uma query |
| `IAxisStreamQuery<out TItem> : IAxisRequest` | uma query que transmite uma sequência de `TItem` em streaming |
| `IAxisEvent` | marker para um fato que já aconteceu |

---

## Handlers

Uma interface de handler por tipo de request. Cada uma retorna `AxisResult` / `AxisResult<TResponse>` (de `Axis`), exceto o handler de stream, que retorna `IAsyncEnumerable<TItem>`.

| Tipo | Método | Assinatura |
|------|--------|-----------|
| `IAxisCommandHandler<in TCommand>` where `TCommand : IAxisCommand` | `HandleAsync` | `Task<AxisResult> HandleAsync(TCommand command)` |
| `IAxisCommandHandler<in TCommand, TResponse>` where `TCommand : IAxisCommand<TResponse>`, `TResponse : IAxisCommandResponse` | `HandleAsync` | `Task<AxisResult<TResponse>> HandleAsync(TCommand command)` |
| `IAxisQueryHandler<in TQuery, TResponse>` where `TQuery : IAxisQuery<TResponse>`, `TResponse : IAxisQueryResponse` | `HandleAsync` | `Task<AxisResult<TResponse>> HandleAsync(TQuery query)` |
| `IAxisEventHandler<in TEvent>` where `TEvent : IAxisEvent` | `HandleAsync` | `Task<AxisResult> HandleAsync(TEvent @event)` |
| `IAxisStreamQueryHandler<in TQuery, out TItem>` where `TQuery : IAxisStreamQuery<TItem>` | `HandleAsync` | `IAsyncEnumerable<TItem> HandleAsync(TQuery query)` |

---

## Fachada de execução — `IAxisMediatorHandler`

A superfície de despacho exposta por `IAxisMediator.Cqrs`. Cada método roteia uma request para seu handler registrado.

| Método | Assinatura | Descrição |
|--------|-----------|-------------|
| `ExecuteAsync` | `Task<AxisResult> ExecuteAsync<TCommand>(TCommand command)` where `TCommand : IAxisCommand` | despacha um comando sem resposta |
| `ExecuteAsync` | `Task<AxisResult<TResponse>> ExecuteAsync<TCommand, TResponse>(TCommand command)` where `TCommand : IAxisCommand<TResponse>`, `TResponse : IAxisCommandResponse` | despacha um comando que retorna `TResponse` |
| `QueryAsync` | `Task<AxisResult<TResponse>> QueryAsync<TQuery, TResponse>(TQuery query)` where `TQuery : IAxisQuery<TResponse>`, `TResponse : IAxisQueryResponse` | despacha uma query de leitura |
| `StreamAsync` | `IAsyncEnumerable<TItem> StreamAsync<TQuery, TItem>(TQuery query)` where `TQuery : IAxisStreamQuery<TItem>` | despacha uma query em streaming |

---

## Mediator e contexto

### `IAxisMediator`

O contexto ambiente de request injetado no código de aplicação.

| Membro | Tipo | Descrição |
|--------|------|-------------|
| `CancellationToken` | `CancellationToken` (get) | token de cancelamento para a request atual |
| `TraceId` | `string` (get) | id de correlação da request |
| `OriginId` | `string?` (get) | id da request/sistema de origem, se houver |
| `JourneyId` | `string?` (get) | id que agrupa uma jornada de usuário multi-passo, se houver |
| `AxisEntityId` | `AxisEntityId?` (get) | a identidade do chamador autenticado, se houver |
| `Cqrs` | `IAxisMediatorHandler` (get) | a fachada de despacho para executar requests |

### `IAxisMediatorAccessor`

Slot de acesso ambiente para o `IAxisMediator` atual (ex.: para armazenamento async-local).

| Membro | Tipo | Descrição |
|--------|------|-------------|
| `AxisMediator` | `IAxisMediator?` (get/set) | o mediator do escopo atual |

### `IAxisMediatorContextAccessor`

Costura mutável usada durante a construção do contexto `IAxisMediator` (ex.: a partir de uma request HTTP).

| Membro | Tipo | Descrição |
|--------|------|-------------|
| `OriginId` | `string?` (get/set) | id de origem a propagar |
| `JourneyId` | `string?` (get/set) | id de jornada a propagar |
| `AxisEntityId` | `AxisEntityId?` (get/set) | a identidade do chamador autenticado |
| `CancellationToken` | `CancellationToken` (get/set) | token de cancelamento para a request |
| `IsAuthenticated` | `bool` (get, impl. padrão) | `true` quando `AxisEntityId != null` |

---

## Pipelines

### Behaviours

Passos transversais envolvidos em torno da execução do handler. Cada um recebe a request, o `AxisPipelineContext` compartilhado e um delegate `next` que invoca o restante do pipeline.

| Tipo | Método | Assinatura |
|------|--------|-----------|
| `IAxisPipelineBehavior<in TRequest>` where `TRequest : IAxisRequest` | `HandleAsync` | `Task<AxisResult> HandleAsync(TRequest request, AxisPipelineContext context, Func<Task<AxisResult>> next)` |
| `IAxisPipelineBehavior<in TRequest, TResponse>` where `TRequest : IAxisRequest`, `TResponse : IAxisResponse` | `HandleAsync` | `Task<AxisResult<TResponse>> HandleAsync(TRequest request, AxisPipelineContext context, Func<Task<AxisResult<TResponse>>> next)` |

### `AxisPipelineContext`

`sealed class` que carrega o estado compartilhado entre behaviours para a execução de uma única request. Itens definidos por um behaviour a montante podem ser lidos pelos de jusante através de chaves tipadas.

| Membro | Assinatura | Descrição |
|--------|-----------|-------------|
| `Items` | `IDictionary<string, object?> Items { get; }` | armazenamento de apoio (chaves string ordinais) |
| `Get<T>` | `T? Get<T>(string key)` | leitura tipada; retorna `default` quando ausente ou de tipo errado |
| `Set<T>` | `void Set<T>(string key, T value)` | escrita tipada |

### `AxisPipelineContextKeys`

`static class` de chaves bem conhecidas escritas no `AxisPipelineContext` por behaviours embutidos.

| Membro | Assinatura | Descrição |
|--------|-----------|-------------|
| `Span` | `const string Span = "axis.pipeline.span"` | o `IAxisSpan` ativo para a request, definido pelo behaviour de telemetria |

---

## Veja também

- [AxisMediator](../../2-ApplicationFlow/AxisMediator/README.md) — o dispatcher concreto, primeiros passos, pipelines e uso de CQRS
- [Documentação completa](README.md) — o mapa deste pacote

---

↩ [Voltar à documentação do AxisMediator.Contracts](README.md)
