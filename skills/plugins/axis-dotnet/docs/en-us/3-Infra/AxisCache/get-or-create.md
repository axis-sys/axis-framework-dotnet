# Get-or-create · `GetOrCreateAsync`

> The cache-aside pattern, in one call. If the key is there, return it. If it isn't, run the factory; if the factory succeeds, store its value and return it. If the factory **fails**, the failure flows through and **nothing is cached**.

```csharp
Task<AxisResult<Person>> result = cache.GetOrCreateAsync(
    key:        $"person:{id}",
    factory:    () => repository.GetByIdAsync(id),       // Task<AxisResult<Person>>
    expiration: TimeSpan.FromMinutes(10));
```

---

## When to use

A read that is **expensive to recompute** (database round-trip, external API call) and **safe to serve from a snapshot** for the chosen TTL. The factory is allowed to be a full `AxisResult` pipeline — including validation, repository calls and projections.

## When *not* to use

| You want to… | Use instead |
|---|---|
| force a refresh, ignoring whatever is cached | `RemoveAsync` then `GetOrCreateAsync` |
| only cache on success **and** swallow failures | wrap with `.RecoverNotFound(...)` from `AxisResult` |
| just write a value (no read) | [`SetAsync`](iaxiscache.md) |
| just read a value (no fill) | [`GetAsync`](iaxiscache.md) |

---

## Behaviour table

| Cache state | Factory outcome | What you get back | What ends up in cache |
|---|---|---|---|
| **hit** | (not called) | `Ok(cachedValue)` | unchanged |
| **miss** | `Ok(value)` | `Ok(value)` | `value`, with `expiration` if provided |
| **miss** | `Error(errors)` | `Error(errors)` | **nothing** — the failure is not memoised |
| **miss** | factory throws | `Error(InternalServerError(ex.Message))` | **nothing** |
| **cancelled** | (not called) | `Error(InternalServerError("…"))` | **nothing** |

Reading `MemoryCacheAdapter.GetOrCreateAsync` directly: a hit returns immediately; on miss, the factory's result is inspected — only `IsSuccess` triggers `memoryCache.Set(key, result.Value[, expiration])`. Cancellation is checked at the start of the method.

---

## Real-world examples

### 1. Person lookup with TTL

```csharp
public Task<AxisResult<Person>> GetByIdAsync(AxisEntityId personId)
    => cache.GetOrCreateAsync(
        key:        $"person:{personId}",
        factory:    () => readerPort.GetByIdAsync(personId),
        expiration: TimeSpan.FromMinutes(10));
```

**Why it pays off:** if the repository returns `NotFound`, that failure is **not** memoised — the next call hits the repository again. You don't accidentally cache "this id doesn't exist" and then miss a freshly inserted row.

### 2. Configuration snapshot (longer TTL)

```csharp
public Task<AxisResult<FeatureFlags>> GetFlagsAsync(string tenantKey)
    => cache.GetOrCreateAsync(
        key:        $"flags:{tenantKey}",
        factory:    () => featureFlagPort.GetForTenantAsync(tenantKey),
        expiration: TimeSpan.FromHours(1));
```

**Why it pays off:** feature flags rarely change; an hour-long TTL slashes the load on the feature-flag service without compromising freshness. The factory is the same code that runs uncached.

### 3. No-TTL cache (lives until eviction or restart)

```csharp
public Task<AxisResult<IReadOnlyList<string>>> GetSupportedCurrenciesAsync()
    => cache.GetOrCreateAsync<IReadOnlyList<string>>(
        key:     "currencies:all",
        factory: () => currencyPort.LoadAllAsync());
        // no expiration argument → no time-based expiry
```

**Why it pays off:** the supported-currency list is effectively immutable for the lifetime of the process; the factory runs at most once per app start, and the cache holds the answer until the `IMemoryCache` evicts it under pressure.

---

## See also

- [The `IAxisCache` contract](iaxiscache.md) — every method
- [`AxisMemoryCache` adapter](memory-adapter.md) — the in-box implementation
- [Custom adapter](custom-adapter.md) — what your `GetOrCreateAsync` must guarantee

---

↩ [Back to AxisCache docs](README.md)
