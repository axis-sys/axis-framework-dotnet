---
name: axis-repository-postgres
description: >
  Implement a repository on Axis — the provider-agnostic internal sealed class that composes over the
  framework's `IAxisDbRepository` by constructor injection, isolates provider-divergent SQL behind a
  per-entity `I{Entity}SqlDialect` seam, maps rows with a `{Entity}DbEntity` ordinal `FromReader`, and is
  wired per aggregate. Use when creating or changing an aggregate's persistence layer: implementing a
  Reader/Write port, mapping a child entity, writing parameterized SQL, or registering DI. This skill is a
  MAP: each row points to the canonical rule in `rules/` — open only the one the context asks for. It does
  NOT restate invariants nor carry code. It does NOT cover the `IAxisDbRepository` package contract itself
  (→ axis-repository), schema / DDL / migrations (→ axis-migrations), the domain ports it implements
  (→ axis-domain-modeling) nor the return monad (→ axis-result).
---

# AxisRepository (Postgres) — rule map (implement a repository)

A **repository** is a provider-agnostic `internal sealed` class that **composes over** the framework's
`IAxisDbRepository` by constructor injection — it never inherits a provider base class. It implements the
domain's Reader/Write ports, runs hand-written parameterized SQL through the framework's execute/get/list
helpers, maps rows with a `{Entity}DbEntity` and its ordinal `FromReader`, and returns `AxisResult` from
every method — it never throws. SQL that differs between engines is isolated behind a per-entity
`I{Entity}SqlDialect` seam with one implementation per targeted provider, so the repository body stays
provider-neutral. This is the reconciled convention: **composition over `IAxisDbRepository` + a dialect
seam**, not a provider-specific base class.

This skill **does not restate** the invariants nor carry code — it **routes**. Each map row points to the
canonical rule (in English) under `rules/conventions/persistence/` (one under `rules/conventions/architecture/`);
open **only** the rule the context requires.

> Before any scaffold, glob an adjacent repository in the same subdomain and mirror it.

## Rule map

### Start here — the repository shape ⭐

| Context / what you were about to write | Rule |
|---|---|
| Compose the repository over `IAxisDbRepository` with a per-entity dialect seam (never a provider base class) | [persistence-repository-composition-dialect-seam](../../rules/conventions/persistence/persistence-repository-composition-dialect-seam.yaml) |
| Where the repository, DbEntity and provider-adapter projects live in the folder/namespace tree | [architecture-one-folder-per-feature](../../rules/conventions/architecture/architecture-one-folder-per-feature.yaml) |

### Ports (Reader / Write)

| Context | Rule |
|---|---|
| Split persistence into `I{Entities}ReaderPort` / `I{Entities}WritePort` returning `AxisResult` over property interfaces | [persistence-reader-write-ports](../../rules/conventions/persistence/persistence-reader-write-ports.yaml) |

### DbEntity mapping

| Context | Rule |
|---|---|
| Map a row with an `internal sealed record {Entity}DbEntity` + a static ordinal `FromReader(DbDataReader)` | [persistence-dbentity-fromreader-ordinal](../../rules/conventions/persistence/persistence-dbentity-fromreader-ordinal.yaml) |
| Single-source the column names and the SELECT projection in `{Entities}Columns` (`.All` in reader-ordinal order) | [persistence-columns-single-source](../../rules/conventions/persistence/persistence-columns-single-source.yaml) |

### SQL discipline

| Context | Rule |
|---|---|
| Pure parameterized SQL only — no ORM, runtime values always bound, interpolation only for compile-time consts | [persistence-sql-parameterized-pure](../../rules/conventions/persistence/persistence-sql-parameterized-pure.yaml) |
| Filter every statement against a tenant-scoped table by the explicit tenant key | [persistence-tenant-scoped-queries](../../rules/conventions/persistence/persistence-tenant-scoped-queries.yaml) |

### DI & Unit of Work

| Context | Rule |
|---|---|
| Register repositories per-BC, scoped, concrete-once with port forwards; the UoW is keyed by AppKey | [persistence-di-registration](../../rules/conventions/persistence/persistence-di-registration.yaml) |
| Commit through the single `IUnitOfWork` — `SaveChangesAsync` once per use case, `RenewAsync` only for lock-refresh reads | [persistence-unit-of-work-contract](../../rules/conventions/persistence/persistence-unit-of-work-contract.yaml) |

### Multi-provider parity (dual-*)

| Context | Rule |
|---|---|
| App targets more than one relational engine — ship and test every persistence change on each provider | [persistence-dual-database-parity](../../rules/conventions/persistence/persistence-dual-database-parity.yaml) |
| Write concurrency code safe on every provider — renew before read-then-act, seam the upserts, prove on each | [persistence-dual-provider-concurrency-traps](../../rules/conventions/persistence/persistence-dual-provider-concurrency-traps.yaml) |

## See also

- [axis-repository](../axis-repository/SKILL.md) — the framework **package contract** (`IAxisDbRepository` + `IAxisUnitOfWork` execution layer) this convention composes over.
- [axis-migrations](../axis-migrations/SKILL.md) — the schema / DDL / `{Entities}Table` / `DbInit` this repository only **consumes**, never defines.
- `axis-domain-modeling` — the Reader/Write ports, property interfaces and value objects this repository implements.
- `axis-dotnet-architect` — the hub; the unbreakable rule (`AxisResult` everywhere, never `try/catch`) this persistence layer is a special case of.
- `axis-rules` — how these rules are authored and maintained (the extraction method from code).
