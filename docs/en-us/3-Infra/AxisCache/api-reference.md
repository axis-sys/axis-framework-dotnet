# API reference

> The complete catalogue, grouped by responsibility. Use it for lookup — each group links back to its detail page.

---

## The contract — `IAxisCache`

| Method | Signature | Description |
|---|---|---|
| `GetAsync<T>` | `Task<AxisResult<T?>> GetAsync<T>(string key)` | read; success-with-`null` is a miss |
| `SetAsync<T>` | `Task<AxisResult> SetAsync<T>(string key, T value, TimeSpan? expiration = null)` | write, overwriting any existing entry |
| `GetOrCreateAsync<T>` | `Task<AxisResult<T>> GetOrCreateAsync<T>(string key, Func<Task<AxisResult<T>>> factory, TimeSpan? expiration = null)` | cache-aside; never caches failures |
| `RemoveAsync` | `Task<AxisResult> RemoveAsync(string key)` | idempotent removal |
| `ExistsAsync` | `Task<AxisResult<bool>> ExistsAsync(string key)` | non-throwing check |

→ [The `IAxisCache` contract](iaxiscache.md) · [Get-or-create pattern](get-or-create.md)

---

## In-memory adapter — `AxisMemoryCache`

| Member | Description |
|---|---|
| `MemoryCacheAdapter(IMemoryCache, IAxisMediatorAccessor)` | constructor; resolves the ambient `CancellationToken` |
| `services.AddAxisMemoryCache()` | DI extension; registers `IMemoryCache` + `IAxisCache → MemoryCacheAdapter` (singleton) |

→ [`AxisMemoryCache` adapter](memory-adapter.md)

---

## SQL-backed adapter — `AxisCache.Postgres` / `AxisCache.MySql`

| Member | Description |
|---|---|
| `AxisCacheRepositorySettings` | `ConnectionString` (required), `L1Ttl` (default 60s; `TimeSpan.Zero` bypasses L1), `RunStartupMigration` (default `true`), `SweepEnabled` (default `true`), `SweepInterval` (default 5min) |
| `services.AddAxisCachePostgres(settings)` | DI extension; registers the Postgres data source/dialect + the shared two-tier core (`IAxisCache → RepositoryCacheAdapter`) |
| `services.AddAxisCacheMySql(settings)` | DI extension; registers the MySQL data source/dialect + the same shared two-tier core |

→ [SQL adapter](sql-adapter.md)

---

## Behaviour contract (for adapters)

| Operation | Cache state | Factory outcome | Returned `AxisResult` | Cache state after |
|---|---|---|---|---|
| `GetAsync<T>` | hit | n/a | `Ok(value)` | unchanged |
| `GetAsync<T>` | miss | n/a | `Ok(null)` | unchanged |
| `SetAsync<T>` | any | n/a | `Ok()` | overwritten |
| `GetOrCreateAsync<T>` | hit | n/a | `Ok(value)` | unchanged |
| `GetOrCreateAsync<T>` | miss | `Ok(value)` | `Ok(value)` | stored |
| `GetOrCreateAsync<T>` | miss | `Error(errors)` | `Error(errors)` | unchanged |
| `RemoveAsync` | any | n/a | `Ok()` | removed |
| `ExistsAsync` | hit | n/a | `Ok(true)` | unchanged |
| `ExistsAsync` | miss | n/a | `Ok(false)` | unchanged |
| any | n/a | adapter threw | `Error(InternalServerError(...))` | unchanged |
| any | n/a | cancelled | `Error(...)` | unchanged |

→ [Custom adapter](custom-adapter.md)

---

## See also

- [Getting started](getting-started.md) — install, register, cache your first value
- [SQL adapter](sql-adapter.md) — the bundled persistent/shared adapter in depth
- [Why AxisCache?](why-axiscache.md) — the case for the abstraction
- [Full documentation](README.md) — the map of the whole documentation

---

↩ [Back to AxisCache docs](README.md)
