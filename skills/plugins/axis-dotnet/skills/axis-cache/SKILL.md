---
name: axis-cache
description: >
  Cache hot data on Axis through `IAxisCache` — one of the swappable infra ports (in-memory
  `MemoryCacheAdapter`, or a two-tier L1-memory-over-durable-L2 SQL adapter on Postgres/MySQL). Use when
  caching stable read-heavy data (permissions, profile, lookups), implementing cache-aside in a query
  handler with `GetOrCreateAsync`, or invalidating entries after a command with `RemoveAsync`. This skill is
  a MAP: each row points to the canonical rule in `rules/` — open only the one the context asks for. It does
  NOT restate invariants nor carry code. It does NOT cover the full monadic composition (→ axis-result), the
  ambient context/`AxisEntityId` (→ axis-mediator), nor the architectural decision of WHERE to cache
  (→ axis-dotnet-architect).
---

# AxisCache — rule map (hot-data caching)

The **cache** is a swappable infra port: application code depends only on `IAxisCache` (five async methods,
every one returning an `AxisResult`), and a single `AddAxis…Cache` call at the composition root decides the
backend. In-box there are two: `AxisMemoryCache` (in-process `IMemoryCache`, Singleton, single-process) and
the two-tier `AxisCache.Repository` (a fast in-process **L1** in front of a durable, cross-instance SQL **L2**
on Postgres or MySQL). The package is 3-infra; the contract lives in `Axis`, the L2 store ports in
`AxisCache.Repository.Ports`.

This skill **does not restate** the invariants nor carry code — it **routes**. Each map row points to the
canonical rule (in English) under `rules/framework/3-infra/axis-cache/`; open **only** the rule the context
requires.

## Rule map

### Start here — route by intent ⭐

| Context / what you were about to write | Rule |
|---|---|
| Cache-aside in one call — read, on miss run a factory, store on success | [cache-get-or-create-cache-aside](../../rules/framework/3-infra/axis-cache/cache-get-or-create-cache-aside.yaml) |
| The port surface — five async methods, all `AxisResult`, no Refresh/batch/tags | [cache-port-contract](../../rules/framework/3-infra/axis-cache/cache-port-contract.yaml) |
| Which backend — memory vs SQL vs your own (Redis/Memcached/hybrid) | [cache-adapter-contract](../../rules/framework/3-infra/axis-cache/cache-adapter-contract.yaml) |

### The contract — the five operations

| Context | Rule |
|---|---|
| A miss is a successful `Ok(null)`, never a failure | [cache-get-miss-is-ok-null](../../rules/framework/3-infra/axis-cache/cache-get-miss-is-ok-null.yaml) |
| `SetAsync` overwrites silently; the TTL is optional | [cache-set-overwrite-optional-ttl](../../rules/framework/3-infra/axis-cache/cache-set-overwrite-optional-ttl.yaml) |
| `GetOrCreateAsync` — a factory failure is never cached | [cache-get-or-create-cache-aside](../../rules/framework/3-infra/axis-cache/cache-get-or-create-cache-aside.yaml) |
| `RemoveAsync` is idempotent — removing a missing key still succeeds | [cache-remove-idempotent](../../rules/framework/3-infra/axis-cache/cache-remove-idempotent.yaml) |

### Cancellation & the swappable port

| Context | Rule |
|---|---|
| The ambient `CancellationToken` (from the mediator), never a parameter | [cache-ambient-cancellation](../../rules/framework/3-infra/axis-cache/cache-ambient-cancellation.yaml) |
| The contract a custom `IAxisCache` (Redis, Memcached, hybrid, test double) must honour | [cache-adapter-contract](../../rules/framework/3-infra/axis-cache/cache-adapter-contract.yaml) |

### In-memory adapter — `AxisMemoryCache`

| Context | Rule |
|---|---|
| Wiring — `AddAxisMemoryCache()` (adapter as Singleton) | [cache-memory-registration](../../rules/framework/3-infra/axis-cache/cache-memory-registration.yaml) |
| Gotcha — a cancelled token **throws**, except inside `GetOrCreateAsync` | [cache-memory-cancellation-propagates](../../rules/framework/3-infra/axis-cache/cache-memory-cancellation-propagates.yaml) |

### Two-tier SQL adapter — `AxisCache.Repository` (Postgres / MySQL)

| Context | Rule |
|---|---|
| The two-tier model — L1 memory over a durable, cross-instance L2 | [cache-repository-two-tier](../../rules/framework/3-infra/axis-cache/cache-repository-two-tier.yaml) |
| A write goes to L2 first with its failure propagated, then warms L1 | [cache-repository-write-propagates-l2-failure](../../rules/framework/3-infra/axis-cache/cache-repository-write-propagates-l2-failure.yaml) |
| L1 lifetime is capped by the value's own remaining life (never outlives L2) | [cache-repository-l1-ttl-bounded](../../rules/framework/3-infra/axis-cache/cache-repository-l1-ttl-bounded.yaml) |
| `L1Ttl = TimeSpan.Zero` bypasses memory — every read hits L2 | [cache-repository-l1-bypass-zero-ttl](../../rules/framework/3-infra/axis-cache/cache-repository-l1-bypass-zero-ttl.yaml) |
| Value (de)serialization never throws — exceptions become an `AxisError` | [cache-serialization-no-throw](../../rules/framework/3-infra/axis-cache/cache-serialization-no-throw.yaml) |

### The L2 store & schema

| Context | Rule |
|---|---|
| The L2 port — `get` / `upsert` / `remove` / `delete-expired`, `CacheEntry` | [cache-store-l2-port](../../rules/framework/3-infra/axis-cache/cache-store-l2-port.yaml) |
| The store never throws; the `CancellationToken` is ambient | [cache-store-never-throws-ambient-ct](../../rules/framework/3-infra/axis-cache/cache-store-never-throws-ambient-ct.yaml) |
| Every value is bound as a parameter, never interpolated into SQL | [cache-store-parameterized-sql](../../rules/framework/3-infra/axis-cache/cache-store-parameterized-sql.yaml) |
| An expired row reads as a miss and is deleted in passing (caller's UTC clock) | [cache-store-expired-read-is-miss](../../rules/framework/3-infra/axis-cache/cache-store-expired-read-is-miss.yaml) |
| L2 connections are autocommit — they never enlist in the business transaction | [cache-connection-autocommit-no-enlist](../../rules/framework/3-infra/axis-cache/cache-connection-autocommit-no-enlist.yaml) |
| The `AXIS_CACHE.CACHE_ENTRIES` schema (one table, framework runner) | [cache-schema-single-table](../../rules/framework/3-infra/axis-cache/cache-schema-single-table.yaml) |
| Dialect divergence — only the upsert differs (`ON CONFLICT` vs `ON DUPLICATE KEY`) | [cache-dialect-upsert-only-divergence](../../rules/framework/3-infra/axis-cache/cache-dialect-upsert-only-divergence.yaml) |

### Wiring & bootstrap

| Context | Rule |
|---|---|
| Core registration — settings singleton, `IAxisCache` / store **Scoped** | [cache-repository-registration-scoped](../../rules/framework/3-infra/axis-cache/cache-repository-registration-scoped.yaml) |
| One storage adapter per process (double-registration throws) | [cache-single-storage-per-process](../../rules/framework/3-infra/axis-cache/cache-single-storage-per-process.yaml) |
| Schema bootstrap worker on startup, idempotent, opt-out | [cache-storage-bootstrap-worker](../../rules/framework/3-infra/axis-cache/cache-storage-bootstrap-worker.yaml) |
| Expiry sweep worker — periodic `DeleteExpiredAsync`, opt-out (`SweepEnabled`/`SweepInterval`) | [cache-store-expired-read-is-miss](../../rules/framework/3-infra/axis-cache/cache-store-expired-read-is-miss.yaml) |

## See also

- `axis-result` — the monad every cache method returns; cache-aside composes with `.ThenAsync` / `.MapAsync`, invalidation with `RemoveAsync` after the write commits.
- `axis-mediator` — the ambient `CancellationToken` the adapters read, and the request scope the two-tier store resolves within.
- `axis-use-case-cqrs` — the query handler that caches a read and the command handler that invalidates it.
- `axis-migrations` / `axis-repository-postgres` — the framework runner and ADO.NET patterns the L2 store reuses.
- `axis-dotnet-architect` — the hub; the swappable-infra-port pattern (`IAxis*` + `AxisResult` + `AddAxis*`) the cache adapters are one instance of. Sibling ports: `axis-bus`, `axis-storage`, `axis-email`.
