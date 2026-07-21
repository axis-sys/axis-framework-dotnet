# Custom adapter · write your own `IAxisCache`

> Swapping the in-memory adapter for Redis, Memcached, or a hybrid (L1 in-memory + L2 distributed) is the whole point of the abstraction. Implement five methods and register your class for `IAxisCache`.

> **Already need cache state to survive a restart or be shared across instances?** Check the bundled [SQL adapter](sql-adapter.md) (`AxisCache.Postgres` / `AxisCache.MySql`) first — it's a first-party two-tier (L1 memory + L2 SQL) implementation of exactly that, no custom code required. Reach for a custom adapter when you specifically need Redis/Memcached-class latency, pub/sub invalidation, or a backend the SQL adapter doesn't target.

```csharp
public class RedisCacheAdapter(IConnectionMultiplexer redis, IAxisMediatorAccessor accessor) : IAxisCache
{
    private readonly CancellationToken _ct =
        accessor.AxisMediator?.CancellationToken ?? CancellationToken.None;

    public Task<AxisResult<T?>> GetAsync<T>(string key)
        => AxisResult.TryAsync(async () =>
        {
            _ct.ThrowIfCancellationRequested();
            var db = redis.GetDatabase();
            var raw = await db.StringGetAsync(key);
            return raw.HasValue ? JsonSerializer.Deserialize<T>(raw!, JsonSerializerOptions.Web) : default;
        });

    // … SetAsync, GetOrCreateAsync, RemoveAsync, ExistsAsync
}
```

---

## When to use

- Production with multiple processes (Redis, Memcached).
- Need for pub/sub-driven invalidation.
- A test double that records every call.
- A hybrid L1+L2 cache (memoise hot keys locally, fall back to Redis).

## When *not* to use

| You want to… | Use instead |
|---|---|
| only run in a single process | the ready-made [`AxisMemoryCache`](memory-adapter.md) |
| survive a restart or share state across instances, on Postgres or MySQL | the ready-made [SQL adapter](sql-adapter.md) — no custom code required |
| use a vendor SDK feature that the contract does not expose | call the SDK directly inside the adapter — keep `IAxisCache` honest |
| add tags / patterns / batch | extend the contract with a new interface and require the *adapter* to implement both |

---

## The contract you must honour

| Behaviour | Required for | Rationale |
|---|---|---|
| Every method returns an `AxisResult`, never throws | all | callers chain through `Then`/`Map` and expect failures as values |
| `GetAsync<T>` returns `Ok(null)` on miss, **not** a failure | `GetAsync` | a miss is normal and must not short-circuit the railway |
| `GetOrCreateAsync` does **not** cache on factory failure | `GetOrCreateAsync` | failures are not memoised; the next call gets a fresh try |
| `RemoveAsync` is success even when the key is missing | `RemoveAsync` | idempotent removal — callers should not have to check first |
| Cancellation flows from `IAxisMediatorAccessor.AxisMediator?.CancellationToken` | all | matches the in-box adapter and the rest of Axis |

---

## Registering your adapter

```csharp
public static class DependencyInjection
{
    public static IServiceCollection AddAxisRedisCache(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<IConnectionMultiplexer>(
            _ => ConnectionMultiplexer.Connect(connectionString));

        services.AddSingleton<IAxisCache, RedisCacheAdapter>();
        return services;
    }
}

// Program.cs
builder.Services.AddAxisRedisCache(builder.Configuration.GetConnectionString("Redis")!);
```

---

## Real-world example — hybrid L1+L2

A two-tier cache: read first from the local `IMemoryCache`, fall through to Redis, write to both on miss.

```csharp
public class HybridCacheAdapter(IMemoryCache l1, IConnectionMultiplexer l2, IAxisMediatorAccessor accessor) : IAxisCache
{
    private readonly CancellationToken _ct =
        accessor.AxisMediator?.CancellationToken ?? CancellationToken.None;

    public async Task<AxisResult<T?>> GetAsync<T>(string key)
    {
        _ct.ThrowIfCancellationRequested();

        if (l1.TryGetValue(key, out T? local))
            return AxisResult.Ok<T?>(local);

        var db = l2.GetDatabase();
        var raw = await db.StringGetAsync(key);
        if (!raw.HasValue) return AxisResult.Ok<T?>(default);

        var value = JsonSerializer.Deserialize<T>(raw!, JsonSerializerOptions.Web);
        l1.Set(key, value, TimeSpan.FromMinutes(1));   // warm the L1
        return AxisResult.Ok<T?>(value);
    }

    // … rest of IAxisCache
}
```

**Why it pays off:** application code stays identical — `cache.GetOrCreateAsync(...)` — while operationally the hot path is in-process memory and only cold misses cross the network. Swapping back to pure Redis is one registration line.

---

## See also

- [The `IAxisCache` contract](iaxiscache.md) — the full surface
- [Get-or-create pattern](get-or-create.md) — the operator your adapter must implement carefully
- [`AxisMemoryCache` adapter](memory-adapter.md) — the in-box single-process reference
- [SQL adapter](sql-adapter.md) — the in-box persistent/shared reference, before you write your own

---

↩ [Back to AxisCache docs](README.md)
