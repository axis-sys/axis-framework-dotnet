# In-memory adapter · `AxisMemoryCache`

> The ready-made implementation of `IAxisCache` over `Microsoft.Extensions.Caching.Memory`. Registered with one line, perfect for single-process apps, tests and the default development experience.

```csharp
using AxisMemoryCache;

services.AddAxisMemoryCache();   // IMemoryCache + IAxisCache singleton
```

---

## When to use

- A single-process app (one API instance, one worker).
- Unit and integration tests where you do not want a network hop.
- Local development; pair with the [SQL adapter](sql-adapter.md) (or a distributed adapter such as Redis) in production.
- A second-tier cache in front of a distributed cache (memoise locally, refresh from the distributed store on miss).

## When *not* to use

| You want to… | Use instead |
|---|---|
| share cache state between processes | the bundled [SQL adapter](sql-adapter.md) (Postgres/MySQL), or a distributed adapter (Redis, Memcached) |
| survive a process restart | the bundled [SQL adapter](sql-adapter.md) — its L2 tier is durable |
| evict by tag/pattern | extend the contract in your adapter |

---

## What gets registered

`DependencyInjection.AddAxisMemoryCache` does exactly two things:

```csharp
public static IServiceCollection AddAxisMemoryCache(this IServiceCollection services)
{
    services.AddMemoryCache();                                      // Microsoft's IMemoryCache
    services.AddSingleton<IAxisCache, MemoryCacheAdapter>();        // the IAxisCache binding
    return services;
}
```

`MemoryCacheAdapter` then needs `IMemoryCache` (provided by `AddMemoryCache`) and `IAxisMediatorAccessor` (provided by `AxisMediator.DependencyInjection`). The mediator gives it the ambient `CancellationToken`.

---

## How each method maps to `IMemoryCache`

| `IAxisCache` | `IMemoryCache` call | Notes |
|---|---|---|
| `GetAsync<T>(key)` | `Get<T>(key)` | wraps in `AxisResult.TryAsync`; returns `Ok(null)` on miss |
| `SetAsync<T>(key, value, expiration?)` | `Set(key, value)` or `Set(key, value, expiration.Value)` | overwrites silently |
| `GetOrCreateAsync<T>(key, factory, expiration?)` | `TryGetValue` + `Set` | factory only stored if `IsSuccess` |
| `RemoveAsync(key)` | `Remove(key)` | success even when the key was missing |
| `ExistsAsync(key)` | `TryGetValue` | returns `Ok(bool)` |

Every method calls `_cancellationToken.ThrowIfCancellationRequested()` first. In the four `TryAsync`-wrapped methods (`GetAsync`, `SetAsync`, `RemoveAsync`, `ExistsAsync`) the resulting `OperationCanceledException` is **rethrown** — `AxisResult.TryAsync` treats cancellation as critical and does *not* convert it to a failed `AxisResult`. `GetOrCreateAsync` is the exception: its own `try/catch (Exception)` catches the cancellation and returns a failed `AxisResult`.

---

## Real-world example — DI wiring and a small handler

```csharp
// Program.cs
builder.Services
    .AddAxisMediator()       // provides IAxisMediatorAccessor (→ CancellationToken)
    .AddAxisLogger()
    .AddAxisMemoryCache();   // registers IAxisCache → MemoryCacheAdapter

// A query handler that uses the cache
public class GetPersonHandler(IAxisCache cache, IPersonReaderPort reader)
{
    public Task<AxisResult<Person>> HandleAsync(GetPersonQuery q)
        => cache.GetOrCreateAsync(
            key:        $"person:{q.PersonId}",
            factory:    () => reader.GetByIdAsync(q.PersonId),
            expiration: TimeSpan.FromMinutes(10));
}
```

**Why it pays off:** the handler is exactly the same code that would talk to Redis. Move to a distributed adapter and the only change is at the composition root.

---

## See also

- [The `IAxisCache` contract](iaxiscache.md) — the interface every adapter implements
- [Get-or-create pattern](get-or-create.md) — the headline operator
- [SQL adapter](sql-adapter.md) — the bundled adapter for when you need to survive a restart or share state
- [Custom adapter](custom-adapter.md) — implement `IAxisCache` for another backend
- [Why AxisCache?](why-axiscache.md) — the case for the abstraction

---

↩ [Back to AxisCache docs](README.md)
