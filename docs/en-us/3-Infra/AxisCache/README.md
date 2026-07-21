# AxisCache — Documentation

> 🌐 [Português (documentação navegável)](../../../pt-br/3-Infra/AxisCache/README.md)

**A tiny `IAxisCache` abstraction** — five async operations (`Get`, `Set`, `GetOrCreate`, `Remove`, `Exists`), every one returning `AxisResult`, with cancellation flowing through `AxisMediator`. Two first-party adapter families ship in the box: a ready-made in-memory adapter (`AxisMemoryCache`) for single-process apps, and a two-tier SQL-backed adapter (`AxisCache.Postgres` / `AxisCache.MySql`) when cache state needs to survive a restart or be shared across instances. Drop in your own for Redis, Memcached or anything else.

```csharp
public Task<AxisResult<Person>> GetByIdAsync(AxisEntityId personId)
    => cache.GetOrCreateAsync(
        key:        $"person:{personId}",
        factory:    () => repository.GetByIdAsync(personId),
        expiration: TimeSpan.FromMinutes(10));
```

Use this page as a **map**: read the trunk below (~5 min) and jump straight to the detail of the group you need — without reading hundreds of lines.

---

## The trunk (read first)

### The interface in 60 seconds

```csharp
public interface IAxisCache
{
    Task<AxisResult<T?>>   GetAsync<T>(string key);
    Task<AxisResult>       SetAsync<T>(string key, T value, TimeSpan? expiration = null);
    Task<AxisResult<T>>    GetOrCreateAsync<T>(string key, Func<Task<AxisResult<T>>> factory, TimeSpan? expiration = null);
    Task<AxisResult>       RemoveAsync(string key);
    Task<AxisResult<bool>> ExistsAsync(string key);
}
```

Five methods. Every result is an `AxisResult`. Cancellation is **implicit** — the adapter pulls the `CancellationToken` from the current `AxisMediator` scope. Your application code never threads a token through cache calls. → **[The `IAxisCache` contract](iaxiscache.md)**

### `GetOrCreateAsync` — the most important operator

The cache-aside pattern, in one call. If the key is there, return it. If it isn't, run the factory, store its `AxisResult<T>` (only on success), return the value. If the factory **fails**, the failure flows through and nothing is cached. → **[Get-or-create pattern](get-or-create.md)**

### In-memory adapter

`AxisMemoryCache` registers `IAxisCache` against `Microsoft.Extensions.Caching.Memory`:

```csharp
services.AddAxisMemoryCache();   // wires IMemoryCache + IAxisCache → MemoryCacheAdapter
```

→ **[`AxisMemoryCache` adapter](memory-adapter.md)**

### SQL-backed adapter

Need the cache to survive a restart or be shared across instances? `AxisCache.Postgres` / `AxisCache.MySql` register the same `IAxisCache`, backed by a fast in-process L1 in front of a durable, shared L2 SQL table:

```csharp
services.AddAxisCachePostgres(new AxisCacheRepositorySettings { ConnectionString = "Host=…" });
```

→ **[SQL adapter](sql-adapter.md)**

### Installation

```
dotnet add package AxisCache             # the abstraction (depends on AxisResult)
dotnet add package AxisMemoryCache       # the in-memory adapter
dotnet add package AxisCache.Postgres    # or AxisCache.MySql — the SQL-backed adapter
```

→ Full guide: **[Getting started](getting-started.md)**

---

## The map (jump to what you need)

| Group | You want to… | Detail |
|---|---|---|
| **Contract · `IAxisCache`** | the five operations and their semantics | [iaxiscache.md](iaxiscache.md) |
| **Get-or-create · `GetOrCreateAsync`** ⭐ | cache-aside pattern with a failable factory | [get-or-create.md](get-or-create.md) |
| **In-memory · `AxisMemoryCache`** | the ready-made `IMemoryCache` adapter | [memory-adapter.md](memory-adapter.md) |
| **SQL-backed · `AxisCache.Postgres` / `AxisCache.MySql`** | the ready-made two-tier adapter that survives a restart and is shared across instances | [sql-adapter.md](sql-adapter.md) |
| **Custom adapter** | write your own (Redis, Memcached, hybrid) | [custom-adapter.md](custom-adapter.md) |
| **Why?** | the case against direct `IDistributedCache` | [why-axiscache.md](why-axiscache.md) |
| **Reference** | every method at a glance | [api-reference.md](api-reference.md) |

**Start here:** [Getting started](getting-started.md) · [The `IAxisCache` contract](iaxiscache.md) · [Why AxisCache?](why-axiscache.md)

**Fundamentals:** [Get-or-create pattern](get-or-create.md) · [`AxisMemoryCache` adapter](memory-adapter.md) · [SQL adapter](sql-adapter.md)

**Reference & extras:** [Custom adapter](custom-adapter.md) · [API reference](api-reference.md)

---

## Design principles

1. **Five methods, no more.** A bigger surface invites micro-optimisations that leak the vendor's model. Keep callers honest.
2. **Every result is an `AxisResult`.** Cache failures are facts, not exceptions. The pipeline decides what to do with them.
3. **Cancellation is implicit.** The adapter pulls the token from `IAxisMediatorAccessor`, so no signature has to carry it.
4. **The adapter is replaceable.** `services.AddAxisMemoryCache()` is one line; swap it for `AddAxisCachePostgres()`/`AddAxisCacheMySql()` — or a custom `AddAxisRedisCache()` — and nothing in the application changes.
5. **`GetOrCreateAsync` is the headline.** The factory is allowed to fail — that failure is **not** cached.

---

## License

Apache 2.0
