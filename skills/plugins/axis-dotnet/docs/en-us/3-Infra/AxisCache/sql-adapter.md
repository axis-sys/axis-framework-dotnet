# SQL adapter · `AxisCache.Postgres` / `AxisCache.MySql`

> The bundled two-tier, SQL-backed `IAxisCache`: a fast in-process L1 (`IMemoryCache`) in front of a durable, shared L2 (a plain SQL table). Same settings type, same DI shape, same runtime behaviour on both databases — the two packages differ only in the DI method name and the connection library underneath.

```csharp
services.AddAxisCachePostgres(new AxisCacheRepositorySettings
{
    ConnectionString = "Host=…",
    L1Ttl            = TimeSpan.FromSeconds(60),
});
```

---

## When to use

Cache state needs to **survive a restart** or be **shared across instances** — the two things the in-memory adapter cannot do. Typical cases: a multi-instance API behind a load balancer where every replica must see the same cached value; a worker that restarts frequently and shouldn't cold-start every lookup; a cache you want to inspect with plain SQL. It reuses the database the application already provisions — no new moving part to operate.

Stay on [`AxisMemoryCache`](memory-adapter.md) for a single-process app, tests, or local development where a network/DB hop per cache miss buys you nothing.

## When *not* to use

| You want to… | Use instead |
|---|---|
| run in a single process with no restart-survival requirement | the ready-made [`AxisMemoryCache`](memory-adapter.md) |
| a sub-millisecond, memory-speed distributed cache (Redis, Memcached) | a [custom adapter](custom-adapter.md) — SQL round-trips are slower than an in-memory store |
| pub/sub-driven invalidation across instances | a [custom adapter](custom-adapter.md) over a backend with native pub/sub |
| target a database other than Postgres or MySQL | a [custom adapter](custom-adapter.md) implementing `IAxisCacheConnectionFactory` / `IAxisCacheSqlDialect` / `IAxisCacheStorageInitializer` against the shared `AxisCache.Repository` core |

---

## The two-tier model

Every read and write goes through two layers:

- **L1** — an in-process `IMemoryCache`, singleton, shared by every request in the instance. Fast, but private to the process and lost on restart.
- **L2** — the SQL store (`ICacheEntryStore`, table `AXIS_CACHE.CACHE_ENTRIES`), the durable source of truth shared by every instance pointed at the same connection string.

Writes go to L2 first with the failure **propagated** — if the database write fails, `SetAsync` fails, full stop — then best-effort warm L1. Reads serve an L1 hit immediately; on an L1 miss they fall back to L2 and rehydrate L1 for at most `L1Ttl` (further bounded by the value's own expiration, so L1 never outlives the authoritative L2 entry). Surviving a restart and being visible to every instance come from L2; the `L1Ttl` window is the *only* staleness a reader can observe across instances.

Set **`L1Ttl = TimeSpan.Zero`** to bypass L1 entirely — every read hits L2 directly. Use this when you need strict cross-instance consistency (no staleness window at all) and can accept the extra round-trip on every read; it's also how the adapter's own integration tests exercise L2 in isolation.

---

## `AxisCacheRepositorySettings`

```csharp
public sealed class AxisCacheRepositorySettings
{
    public required string ConnectionString { get; init; }
    public TimeSpan L1Ttl { get; init; } = TimeSpan.FromSeconds(60);
    public bool RunStartupMigration { get; init; } = true;
    public bool SweepEnabled { get; init; } = true;
    public TimeSpan SweepInterval { get; init; } = TimeSpan.FromMinutes(5);
}
```

| Property | Default | Meaning |
|---|---|---|
| `ConnectionString` | *(required)* | Points at the L2 SQL store — normally the same database the application already provisions for its own data. |
| `L1Ttl` | `TimeSpan.FromSeconds(60)` | How long L1 trusts a value before falling back to L2. `TimeSpan.Zero` disables L1 (every read hits L2). |
| `RunStartupMigration` | `true` | When true, a hosted service creates the L2 schema on startup (idempotent). Turn off for hosts that must not touch the database at boot — e.g. a test host that fakes the storage ports. |
| `SweepEnabled` | `true` | When true, a hosted service periodically deletes expired L2 rows (`DeleteExpiredAsync`) so reclamation does not depend on a key being read again. Turn off for hosts that must not run background database work, or that delegate the sweep to a dedicated process — expiry is still honoured passively on every read. |
| `SweepInterval` | `TimeSpan.FromMinutes(5)` | How often the sweep worker deletes expired L2 rows while `SweepEnabled` is set. |

---

## What gets registered

Both `AddAxisCachePostgres` and `AddAxisCacheMySql` provide the dialect-specific pieces and then delegate to the shared `AddAxisCacheRepositoryCore`:

| Service | Lifetime | Provided by |
|---|---|---|
| `AxisCacheRepositorySettings` | singleton | core |
| `IMemoryCache` | singleton | core (`AddMemoryCache()`) — the L1 tier |
| `TimeProvider` | singleton | core (`TimeProvider.System`, via `TryAddSingleton`) |
| `ICacheEntryStore` | scoped | core (`CacheEntryStore`) — the L2 tier |
| `IAxisCache` | scoped | core (`RepositoryCacheAdapter`) — the two-tier adapter itself |
| `IAxisCacheConnectionFactory` | singleton | the storage adapter (`PostgresCacheConnectionFactory` / `MySqlCacheConnectionFactory`) — opens L2 connections from a pooled data source |
| `IAxisCacheSqlDialect` | singleton | the storage adapter (`PostgresCacheSqlDialect` / `MySqlCacheSqlDialect`) — the one SQL statement that genuinely differs between databases (the upsert) |
| `IAxisCacheStorageInitializer` | singleton | the storage adapter (`PostgresCacheStorageInitializer` / `MySqlCacheStorageInitializer`) — creates the L2 schema |
| `AxisCacheStorageInitializerWorker` | hosted service | core, only when `RunStartupMigration` is `true` — runs the initializer once on startup |
| `AxisCacheSweepWorker` | hosted service | core, only when `SweepEnabled` is `true` (the default) — periodically calls `DeleteExpiredAsync` every `SweepInterval` |

`IAxisCache` and `ICacheEntryStore` are scoped (not singleton) because the store depends on `IAxisLogger<T>`, which in turn depends on the scoped, ambient `IAxisMediator`. L1 itself is still the shared singleton `IMemoryCache`, so caching still spans requests — only the adapter facade is resolved per scope.

---

## `AddAxisCachePostgres`

```csharp
using AxisCache.Postgres;

builder.Services.AddAxisCachePostgres(new AxisCacheRepositorySettings
{
    ConnectionString = builder.Configuration.GetConnectionString("Postgres")!,
    L1Ttl            = TimeSpan.FromSeconds(60),
});
```

Registers a pooled `NpgsqlDataSource` behind `IAxisCacheConnectionFactory`, `PostgresCacheSqlDialect`, `PostgresCacheStorageInitializer`, then calls `AddAxisCacheRepositoryCore(settings)`.

## `AddAxisCacheMySql`

```csharp
using AxisCache.MySql;

builder.Services.AddAxisCacheMySql(new AxisCacheRepositorySettings
{
    ConnectionString = builder.Configuration.GetConnectionString("MySql")!,
    L1Ttl            = TimeSpan.FromSeconds(60),
});
```

Registers a pooled `MySqlDataSource` behind `IAxisCacheConnectionFactory`, `MySqlCacheSqlDialect`, `MySqlCacheStorageInitializer`, then calls `AddAxisCacheRepositoryCore(settings)` — the exact same core call `AddAxisCachePostgres` makes.

---

## Single storage per process

`AxisCache` supports a single storage backend per process by design. Both `AddAxisCachePostgres` and `AddAxisCacheMySql` check whether `AxisCacheRepositorySettings` is already registered and **throw `InvalidOperationException`** if it is — whether the earlier call was to the same method or the other one. Call exactly one of them, exactly once, at application startup. This mirrors the equivalent guard in `AxisSaga`'s storage adapters.

---

## Schema bootstrap

The L2 table lives in the `AXIS_CACHE` schema and is declared once, dialect-agnostically, in the shared core; each adapter renders it with its own SQL dialect. Two ways it gets applied:

- **Automatically** — when `RunStartupMigration` is `true` (the default), `AxisCacheStorageInitializerWorker` (a `BackgroundService`) runs `IAxisCacheStorageInitializer.InitializeAsync()` once after the host starts. Idempotent — safe on every restart.
- **Explicitly** — call `Persistence.AxisCacheMigrations.InitializePostgresAsync(connectionString)` or `Persistence.AxisCacheMigrations.InitializeMySqlAsync(connectionString)` directly (from `AxisCache.Postgres.Persistence` / `AxisCache.MySql.Persistence`). This is how test fixtures provision the schema against a Testcontainers instance before the DI-registered worker would otherwise run, and how you'd migrate ahead of deploying to a host with `RunStartupMigration = false`.

---

## Real-world example — production wiring

```csharp
// Program.cs
builder.Services
    .AddAxisMediator()
    .AddAxisLogger()
    .AddAxisCachePostgres(new AxisCacheRepositorySettings
    {
        ConnectionString    = builder.Configuration.GetConnectionString("Postgres")!,
        L1Ttl               = TimeSpan.FromSeconds(30),
        RunStartupMigration = true,
    });

// A query handler — identical to the one written against AxisMemoryCache
public class GetPersonHandler(IAxisCache cache, IPersonReaderPort reader)
{
    public Task<AxisResult<Person>> HandleAsync(GetPersonQuery q)
        => cache.GetOrCreateAsync(
            key:        $"person:{q.PersonId}",
            factory:    () => reader.GetByIdAsync(q.PersonId),
            expiration: TimeSpan.FromMinutes(10));
}
```

**Why it pays off:** swapping `AddAxisMemoryCache()` for `AddAxisCachePostgres(settings)` (or `AddAxisCacheMySql(settings)`) is the only change at the composition root. Every handler that calls `IAxisCache` — cache-aside reads, `RemoveAsync` invalidation — keeps working unmodified, but the cached values now survive a restart and are shared by every instance pointed at the same database.

---

## See also

- [`AxisMemoryCache` adapter](memory-adapter.md) — the single-process, in-memory alternative this adapter extends with a durable, shared L2
- [The `IAxisCache` contract](iaxiscache.md) — the interface both adapters implement
- [Get-or-create pattern](get-or-create.md) — the headline operator, unchanged by which adapter is registered

---

↩ [Back to AxisCache docs](README.md)
