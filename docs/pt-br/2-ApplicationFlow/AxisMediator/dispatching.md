# Despachando · `IAxisMediatorHandler`

> Quatro métodos em `mediator.Cqrs` — um por formato de request. O dispatcher resolve o handler, constrói o pipeline, executa e (para formatos que retornam `AxisResult`) loga o desfecho.

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

## Quando usar

Qualquer caminho de código que precise invocar um handler — controllers, middlewares de borda, handlers de integração, até outros handlers (com parcimônia). O dispatcher é a **única** forma de handlers rodarem dentro do pipeline; chamar `handler.HandleAsync(...)` direto pula cada behaviour.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| publicar um evento | [`AxisBus.PublishAsync`](../AxisBus/README.md) |
| chamar uma porta (sua própria interface) | injete a porta; não é formato de request |
| espalhar a chamada entre serviços | uma [`Saga`](../AxisSaga/README.md) ou uma invocação out-of-process |

---

## Os quatro métodos

| Método | Constraints | Retorna | Interface de handler |
|---|---|---|---|
| `ExecuteAsync<TCommand>(command)` | `TCommand : IAxisCommand` | `Task<AxisResult>` | `IAxisCommandHandler<TCommand>` |
| `ExecuteAsync<TCommand, TResponse>(command)` | `TCommand : IAxisCommand<TResponse>`, `TResponse : IAxisCommandResponse` | `Task<AxisResult<TResponse>>` | `IAxisCommandHandler<TCommand, TResponse>` |
| `QueryAsync<TQuery, TResponse>(query)` | `TQuery : IAxisQuery<TResponse>`, `TResponse : IAxisQueryResponse` | `Task<AxisResult<TResponse>>` | `IAxisQueryHandler<TQuery, TResponse>` |
| `StreamAsync<TQuery, TItem>(query)` | `TQuery : IAxisStreamQuery<TItem>` | `IAsyncEnumerable<TItem>` | `IAxisStreamQueryHandler<TQuery, TItem>` |

---

## O que o dispatcher faz

Lendo `AxisMediatorHandler` direto:

1. **Resolve o handler** do `IServiceProvider`. Se não achar:
   - `ExecuteAsync` / `QueryAsync` → `AxisError.NotFound($"HANDLER_NOT_FOUND_{typeof(TRequest).Name}")`.
   - `StreamAsync` → lança `InvalidOperationException` com a mesma mensagem (você não pode retornar erro de um `IAsyncEnumerable<TItem>` aqui).
2. **Constrói o pipeline** a partir de `IServiceProvider.GetServices<IAxisPipelineBehavior<...>>()` — invertido para que o **primeiro registrado** seja o wrapper mais externo.
3. **Executa o pipeline**, depois chama `LogResult<TRequest>` (sucesso → `LogInformation`, falha → `LogError` com `RequestName`, `TraceId`, `JourneyId`, e a `AxisErrorList` completa).
4. **Retorna** o resultado (ou yield itens para streams).

> Pipelines são **por tipo de request**. `IAxisPipelineBehavior<CreateOrderCommand>` é seu próprio tipo — registrar um open generic (`IAxisPipelineBehavior<>`) te dá todos eles de uma vez.

---

## Exemplos reais

### 1. Command de um controller

```csharp
public class OrdersController(IAxisMediator mediator) : ControllerBase
{
    [HttpPost]
    public Task<AxisResult<CreateOrderResponse>> CreateAsync(CreateOrderCommand cmd)
        => mediator.Cqrs.ExecuteAsync<CreateOrderCommand, CreateOrderResponse>(cmd);
}
```

**Por que compensa:** o controller é um forward de uma linha para o dispatcher; o pipeline (validação, logging, telemetria, performance, seus behaviours custom) embrulha a chamada automaticamente.

### 2. Query num job de background

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

**Por que compensa:** queries rodam pelo mesmo pipeline que requests HTTP — mesmo logging, mesma telemetria. O job fica pequeno.

### 3. Stream sobre um export

```csharp
await foreach (var row in mediator.Cqrs.StreamAsync<ExportPeopleQuery, PersonRow>(query))
    await writer.WriteAsync(row);
```

**Por que compensa:** o export usa um `IAsyncEnumerable<PersonRow>` de ponta a ponta; o produtor consegue stream do banco sem buffering, o consumidor escreve uma linha por vez.

### 4. Despachando de outro handler (com parcimônia)

```csharp
public class CreateOrderHandler(IAxisMediator mediator, ...)
    : IAxisCommandHandler<CreateOrderCommand, CreateOrderResponse>
{
    public Task<AxisResult<CreateOrderResponse>> HandleAsync(CreateOrderCommand cmd)
        => mediator.Cqrs.QueryAsync<GetCustomerQuery, GetCustomerResponse>(new(cmd.CustomerId))
            .ThenAsync(customer => /* … */);
}
```

**Por que compensa:** às vezes a forma mais limpa de ler um cliente é usar a mesma query que a API usa. O dispatcher loga e traça a chamada interna também — um sub-span "de graça".

> Use com parcimônia. Um handler que chama vários outros geralmente é uma saga disfarçada.

---

## Veja também

- [CQRS · commands, queries, streams, eventos](cqrs.md) — os formatos de request
- [Pipeline behaviours](pipeline-behaviors.md) — o que embrulha cada dispatch
- [Pipeline context](pipeline-context.md) — compartilhe valores entre behaviours
- [Registro e scanning](registration.md) — como handlers entram na DI

---

↩ [Voltar à documentação do AxisMediator](README.md)
