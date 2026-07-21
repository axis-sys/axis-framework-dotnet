# Getting started · installation and usage

> Install the abstraction and an adapter, register them in DI, and cache your first value in under five minutes.

---

## Installation

```
dotnet add package AxisCache             # the abstraction
dotnet add package AxisMemoryCache       # in-memory adapter (Microsoft.Extensions.Caching.Memory)
```

`AxisCache` depends on `AxisResult` (for the return type). `AxisMemoryCache` adds `Microsoft.Extensions.Caching.Memory`. Both are small and dependency-light.

Need the cache to survive a restart or be shared across instances instead? Install the bundled two-tier SQL adapter instead of (or alongside) `AxisMemoryCache`:

```
dotnet add package AxisCache.Postgres    # SQL-backed adapter over PostgreSQL
dotnet add package AxisCache.MySql       # SQL-backed adapter over MySQL
```

See [SQL adapter](sql-adapter.md) for the settings shape and DI wiring.

---

## Registering the adapter

```csharp
using AxisMemoryCache;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAxisMemoryCache();   // registers IMemoryCache + IAxisCache as singleton
```

> The adapter resolves the ambient `CancellationToken` from `IAxisMediatorAccessor`. Make sure `AxisMediator` is wired up — otherwise cancellation falls back to `CancellationToken.None`.

---

## Reading and writing

```csharp
public class PersonService(IAxisCache cache, IPersonReaderPort repo)
{
    public Task<AxisResult<Person?>> GetCachedAsync(AxisEntityId id)
        => cache.GetAsync<Person>($"person:{id}");

    public Task<AxisResult> CacheAsync(Person person)
        => cache.SetAsync($"person:{person.PersonId}", person, TimeSpan.FromMinutes(10));

    public Task<AxisResult> InvalidateAsync(AxisEntityId id)
        => cache.RemoveAsync($"person:{id}");
}
```

> `GetAsync<T>` returns `AxisResult<T?>` — success-with-`null` is a *miss*, not a failure. Failures only show up when the adapter itself blows up (or the operation is cancelled).

---

## The headline: `GetOrCreateAsync`

```csharp
public Task<AxisResult<Person>> GetByIdAsync(AxisEntityId id)
    => cache.GetOrCreateAsync(
        key:        $"person:{id}",
        factory:    () => repository.GetByIdAsync(id),      // Task<AxisResult<Person>>
        expiration: TimeSpan.FromMinutes(10));
```

**Why it pays off:** the cache-aside pattern collapses to a single call, the factory can fail (and is *not* cached on failure), and the cache miss path stays out of the calling site. Add or remove caching by flipping one line — the rest of the pipeline doesn't change.

---

## See also

- [The `IAxisCache` contract](iaxiscache.md) — every method, its semantics and failure modes
- [Get-or-create pattern](get-or-create.md) — the cache-aside operator in depth
- [`AxisMemoryCache` adapter](memory-adapter.md) — what `AddAxisMemoryCache()` registers
- [SQL adapter](sql-adapter.md) — what `AddAxisCachePostgres()` / `AddAxisCacheMySql()` register
- [Custom adapter](custom-adapter.md) — implement `IAxisCache` for Redis or your storage of choice
- [Why AxisCache?](why-axiscache.md) — the case against direct `IDistributedCache`
- [API reference](api-reference.md) — every method in one place

---

↩ [Back to AxisCache docs](README.md)
