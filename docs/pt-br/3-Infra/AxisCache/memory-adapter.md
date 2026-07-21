# Adapter in-memory · `AxisMemoryCache`

> A implementação pronta de `IAxisCache` em cima de `Microsoft.Extensions.Caching.Memory`. Registrada com uma linha, perfeita para apps single-process, testes e a experiência de desenvolvimento padrão.

```csharp
using AxisMemoryCache;

services.AddAxisMemoryCache();   // IMemoryCache + IAxisCache singleton
```

---

## Quando usar

- Um app single-process (uma instância de API, um worker).
- Testes unitários e de integração onde você não quer ir pela rede.
- Desenvolvimento local; pareie com o [adapter SQL](sql-adapter.md) (ou um adapter distribuído como Redis) em produção.
- Cache de segundo nível na frente de um cache distribuído (memoize localmente, refresh no store distribuído em miss).

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| compartilhar estado de cache entre processos | o [adapter SQL](sql-adapter.md) embutido (Postgres/MySQL), ou um adapter distribuído (Redis, Memcached) |
| sobreviver a restart de processo | o [adapter SQL](sql-adapter.md) embutido — o nível L2 dele é durável |
| evictar por tag/padrão | estenda o contrato no seu adapter |

---

## O que é registrado

`DependencyInjection.AddAxisMemoryCache` faz exatamente duas coisas:

```csharp
public static IServiceCollection AddAxisMemoryCache(this IServiceCollection services)
{
    services.AddMemoryCache();                                      // IMemoryCache da Microsoft
    services.AddSingleton<IAxisCache, MemoryCacheAdapter>();        // o binding de IAxisCache
    return services;
}
```

O `MemoryCacheAdapter` então precisa de `IMemoryCache` (fornecido por `AddMemoryCache`) e `IAxisMediatorAccessor` (fornecido por `AxisMediator.DependencyInjection`). O mediator entrega o `CancellationToken` ambiente.

---

## Como cada método mapeia para `IMemoryCache`

| `IAxisCache` | Chamada em `IMemoryCache` | Notas |
|---|---|---|
| `GetAsync<T>(key)` | `Get<T>(key)` | embrulha em `AxisResult.TryAsync`; retorna `Ok(null)` em miss |
| `SetAsync<T>(key, value, expiration?)` | `Set(key, value)` ou `Set(key, value, expiration.Value)` | sobrescreve silenciosamente |
| `GetOrCreateAsync<T>(key, factory, expiration?)` | `TryGetValue` + `Set` | factory só é armazenada se `IsSuccess` |
| `RemoveAsync(key)` | `Remove(key)` | sucesso mesmo quando a chave estava ausente |
| `ExistsAsync(key)` | `TryGetValue` | retorna `Ok(bool)` |

Todo método chama `_cancellationToken.ThrowIfCancellationRequested()` primeiro. Nos quatro métodos embrulhados em `TryAsync` (`GetAsync`, `SetAsync`, `RemoveAsync`, `ExistsAsync`) o `OperationCanceledException` resultante é **relançado** — o `AxisResult.TryAsync` trata cancelamento como crítico e *não* o converte em `AxisResult` falho. O `GetOrCreateAsync` é a exceção: o `try/catch (Exception)` próprio dele captura o cancelamento e retorna um `AxisResult` falho.

---

## Exemplo real — fiação de DI e um pequeno handler

```csharp
// Program.cs
builder.Services
    .AddAxisMediator()       // fornece IAxisMediatorAccessor (→ CancellationToken)
    .AddAxisLogger()
    .AddAxisMemoryCache();   // registra IAxisCache → MemoryCacheAdapter

// Um query handler que usa o cache
public class GetPersonHandler(IAxisCache cache, IPersonReaderPort reader)
{
    public Task<AxisResult<Person>> HandleAsync(GetPersonQuery q)
        => cache.GetOrCreateAsync(
            key:        $"person:{q.PersonId}",
            factory:    () => reader.GetByIdAsync(q.PersonId),
            expiration: TimeSpan.FromMinutes(10));
}
```

**Por que compensa:** o handler é exatamente o mesmo código que falaria com o Redis. Mude para um adapter distribuído e a única alteração está na raiz de composição.

---

## Veja também

- [O contrato `IAxisCache`](iaxiscache.md) — a interface que todo adapter implementa
- [Padrão get-or-create](get-or-create.md) — o operador-destaque
- [Adapter SQL](sql-adapter.md) — o adapter embutido para quando você precisa sobreviver a restart ou compartilhar estado
- [Adapter custom](custom-adapter.md) — implemente `IAxisCache` para outro backend
- [Por que AxisCache?](why-axiscache.md) — o argumento pela abstração

---

↩ [Voltar à documentação do AxisCache](README.md)
