# O mediator e os accessors · `IAxisMediator`, `IAxisMediatorAccessor`, `IAxisMediatorContextAccessor`

> Três camadas. `IAxisMediator` é o contexto ambiente que cada handler lê. `IAxisMediatorAccessor` é o **último** `IAxisMediator` que rodou (singleton, usado por adapters que não têm scope DI). `IAxisMediatorContextAccessor` é a **fonte da verdade backed por `AsyncLocal`** para `TraceId`/`OriginId`/`JourneyId`/`AxisEntityId`/`CancellationToken`.

```csharp
public class CreateOrderHandler(IAxisMediator mediator, ...)
{
    public Task<AxisResult<CreateOrderResponse>> HandleAsync(CreateOrderCommand cmd)
    {
        // mediator carrega tudo que você precisa
        logger.LogInformation("Traced as {TraceId}", mediator.TraceId);
        // …
    }
}
```

---

## Quando usar qual

| Tipo | Lifetime | Use quando |
|---|---|---|
| `IAxisMediator` | scoped | o **caminho feliz** — injete em handlers / behaviours / serviços de domínio |
| `IAxisMediatorAccessor` | singleton | adapters que precisam do mediator *atualmente rodando* e **não podem** depender do scope (ex.: `MemoryCacheAdapter.CancellationToken`) |
| `IAxisMediatorContextAccessor` | singleton | a **borda** define os valores ambiente (`OriginId`, `JourneyId` etc.) na entrada do request |

---

## `IAxisMediator`

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

Lendo `AxisMediator` direto:

- Propriedades delegam para `IAxisMediatorContextAccessor`.
- `TraceId` é capturado **uma vez** na construção: se `Activity.Current` existir, seu `TraceId`; caso contrário, um `Guid.NewGuid().ToString()` fresco.
- O construtor define `_accessor.AxisMediator = this`; `Dispose()` limpa.

## `IAxisMediatorAccessor`

```csharp
public interface IAxisMediatorAccessor
{
    IAxisMediator? AxisMediator { get; set; }
}
```

O accessor mantém o **último construído** `IAxisMediator`. Adapters que são singletons (ex.: `MemoryCacheAdapter`) leem `accessor.AxisMediator?.CancellationToken` no lugar de injetar `IAxisMediator` direto (que é scoped).

## `IAxisMediatorContextAccessor`

```csharp
public interface IAxisMediatorContextAccessor
{
    string? OriginId { get; set; }
    string? JourneyId { get; set; }
    AxisEntityId? AxisEntityId { get; set; }
    CancellationToken CancellationToken { get; set; }
    bool IsAuthenticated => AxisEntityId != null;
}
```

A implementação padrão armazena cada propriedade num `AsyncLocal<T>`, então um middleware por-request que define `OriginId = "rest"` carrega esse valor a cada continuation aguardada.

---

## Exemplos reais

### 1. Definindo o contexto ambiente na borda HTTP

```csharp
app.Use(async (ctx, next) =>
{
    var contextAccessor = ctx.RequestServices.GetRequiredService<IAxisMediatorContextAccessor>();

    contextAccessor.OriginId = "rest";
    contextAccessor.CancellationToken = ctx.RequestAborted;

    if (ctx.User.Identity?.IsAuthenticated == true
        && AxisEntityId.TryParse(ctx.User.FindFirst("sub")?.Value, out var id))
    {
        contextAccessor.AxisEntityId = id;
    }

    await next();
});
```

**Por que compensa:** um middleware preenche o contexto ambiente uma vez. Todo handler, validator, adapter e behaviour a jusante lê dele — sem threading de parâmetro, sem `HttpContext` vazando para código de domínio.

### 2. Adapter singleton lendo o token ambiente

```csharp
public class MemoryCacheAdapter(IMemoryCache memoryCache, IAxisMediatorAccessor accessor) : IAxisCache
{
    private readonly CancellationToken _ct = accessor.AxisMediator?.CancellationToken ?? CancellationToken.None;
    // … todo método chama _ct.ThrowIfCancellationRequested()
}
```

**Por que compensa:** o adapter de cache é singleton (um por app), mas cada chamada ainda respeita o token de cancelamento **do request**. O accessor faz a ponte entre o mismatch de lifetimes.

---

## Veja também

- [Despachando · `IAxisMediatorHandler`](dispatching.md) — o que `mediator.Cqrs` faz
- [Registro e scanning](registration.md) — o que `AddAxisMediator()` registra
- [CQRS · commands, queries, streams, eventos](cqrs.md) — os formatos que o dispatcher trata
- [`AxisLogger`](../../1-Observability/AxisLogger/README.md) — usa `OriginId`/`TraceId`/`JourneyId` para enrichment de log

---

↩ [Voltar à documentação do AxisMediator](README.md)
