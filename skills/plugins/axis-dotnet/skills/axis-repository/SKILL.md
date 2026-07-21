---
name: axis-repository
description: >
  Data access on Axis with `IAxisDbRepository` + `IAxisUnitOfWork` and the Postgres / MySQL adapters —
  parameterized ADO.NET (never an ORM) where every operation returns `AxisResult` and never throws. Use when
  implementing or changing a repository's execution layer: running SQL through the four execution methods,
  binding parameters and mapping a `DbDataReader`, wiring keyed multi-database DI, or reasoning about
  transactions, transient retry and error classification. This skill is a MAP: each row points to the
  canonical rule in `rules/` — open only the one the context asks for. It does NOT restate invariants nor
  carry code. It does NOT cover schema / DDL / migrations (→ axis-migrations), the domain ports the repository
  implements (→ axis-domain-modeling), the return monad (→ axis-result) nor the ambient context (→ axis-mediator).
---

# AxisRepository — rule map (data access & unit of work)

A **repository** on Axis runs parameterized SQL over the common ADO.NET abstractions and returns `AxisResult`,
never throwing for an expected outcome. `IAxisDbRepository` is the dialect-agnostic execution port
(Execute / ExecuteCount / Get / List); `IAxisUnitOfWork` is the transaction lifecycle; `AxisRepositoryBase`
holds the shared retry / fault / error-mapping core, and the Postgres and MySQL adapters supply only the
provider-typed classification. The package is 3-infra; the two adapters (`AxisRepository.Postgres` via Npgsql,
`AxisRepository.MySql` via MySqlConnector) swap under one keyed DI registration.

This skill **does not restate** the invariants nor carry code — it **routes**. Each map row points to the
canonical rule (in English) under `rules/framework/3-infra/axis-repository/`; open **only** the rule the
context requires.

## Rule map

### Start here — the contract ⭐

| Context / what you were about to write | Rule |
|---|---|
| The core promise — everything returns `AxisResult`, expected failures never throw | [repository-returns-result-never-throws](../../rules/framework/3-infra/axis-repository/repository-returns-result-never-throws.yaml) |
| The execution port — the four methods and their result semantics (incl. `NotFound`) | [repository-execution-surface](../../rules/framework/3-infra/axis-repository/repository-execution-surface.yaml) |
| Binding params and mapping rows without naming a provider type | [repository-dialect-agnostic-io](../../rules/framework/3-infra/axis-repository/repository-dialect-agnostic-io.yaml) |
| Inheritance (provider-typed) vs composition (binder) — which surface to use | [repository-base-dual-surface](../../rules/framework/3-infra/axis-repository/repository-base-dual-surface.yaml) |

### Exception boundary (map, or deliberately propagate)

| Context | Rule |
|---|---|
| Where the `CancellationToken` comes from — ambient, never a parameter | [repository-ambient-cancellation](../../rules/framework/3-infra/axis-repository/repository-ambient-cancellation.yaml) |
| A cancelled call throws `OperationCanceledException`, never a mapped error | [repository-cancellation-rethrown](../../rules/framework/3-infra/axis-repository/repository-cancellation-rethrown.yaml) |
| Only `DbException` is mapped; a fatal defect propagates | [repository-fatal-exception-propagates](../../rules/framework/3-infra/axis-repository/repository-fatal-exception-propagates.yaml) |
| A failed lazy connection start surfaces as `AxisDbException` (so the base maps it) | [repository-lazy-start-surfaces-dberror](../../rules/framework/3-infra/axis-repository/repository-lazy-start-surfaces-dberror.yaml) |

### Error classification (DbException → typed AxisError)

| Context | Rule |
|---|---|
| The five abstract dialect hooks the base defers to | [repository-error-classification-hooks](../../rules/framework/3-infra/axis-repository/repository-error-classification-hooks.yaml) |
| A unique-constraint violation → `Conflict` (caller-overridable code) | [repository-duplicate-key-conflict](../../rules/framework/3-infra/axis-repository/repository-duplicate-key-conflict.yaml) |
| A missing relation/schema → transient `ServiceUnavailable`, no error log | [repository-schema-not-ready](../../rules/framework/3-infra/axis-repository/repository-schema-not-ready.yaml) |
| An unclassified failure → logged `InternalServerError`, never reclassified | [repository-generic-execution-error](../../rules/framework/3-infra/axis-repository/repository-generic-execution-error.yaml) |
| A surfaced transient → logged, transient `ServiceUnavailable` | [repository-transient-typed-error](../../rules/framework/3-infra/axis-repository/repository-transient-typed-error.yaml) |

### Retry & fault semantics

| Context | Rule |
|---|---|
| When a transient is retried in place vs surfaced for full replay (the write-safety rule) | [repository-transient-retry-write-safety](../../rules/framework/3-infra/axis-repository/repository-transient-retry-write-safety.yaml) |
| After any command error the whole unit of work short-circuits | [repository-faulted-transaction-guard](../../rules/framework/3-infra/axis-repository/repository-faulted-transaction-guard.yaml) |

### Unit of work & transactions

| Context | Rule |
|---|---|
| The transaction lifecycle — `Start` / `SaveChanges` / `Rollback`, dispose | [repository-unitofwork-transaction-surface](../../rules/framework/3-infra/axis-repository/repository-unitofwork-transaction-surface.yaml) |
| Run work on the railway — commit on success, roll back on failure/exception | [repository-unitofwork-in-transaction-railway](../../rules/framework/3-infra/axis-repository/repository-unitofwork-in-transaction-railway.yaml) |
| Release a pooled connection mid-unit-of-work before slow external I/O | [repository-unitofwork-release-connection](../../rules/framework/3-infra/axis-repository/repository-unitofwork-release-connection.yaml) |
| The provider-typed seam — `NewCommandAsync` + the fault/write flags | [repository-dbunitofwork-dialect-seam](../../rules/framework/3-infra/axis-repository/repository-dbunitofwork-dialect-seam.yaml) |

### Registration & keyed multi-database

| Context | Rule |
|---|---|
| `AddAxis{Provider}UnitOfWork` — keyed data source + unit of work behind both interfaces | [repository-keyed-unit-of-work-registration](../../rules/framework/3-infra/axis-repository/repository-keyed-unit-of-work-registration.yaml) |
| One cached unit of work per key per scope (both interfaces share a connection) | [repository-unit-of-work-provider-per-scope](../../rules/framework/3-infra/axis-repository/repository-unit-of-work-provider-per-scope.yaml) |
| `AddAxis{Provider}DbRepository` — the executor as scoped `IAxisDbRepository` | [repository-keyed-db-repository-registration](../../rules/framework/3-infra/axis-repository/repository-keyed-db-repository-registration.yaml) |
| Data source singleton + unit of work scoped (Npgsql vs MySqlConnector mechanics) | [repository-datasource-lifetimes](../../rules/framework/3-infra/axis-repository/repository-datasource-lifetimes.yaml) |

### Provider adapters (Postgres / MySQL)

| Context | Rule |
|---|---|
| The four-part shape of a provider adapter | [repository-provider-adapter-shape](../../rules/framework/3-infra/axis-repository/repository-provider-adapter-shape.yaml) |
| Postgres classification by `NpgsqlException.SqlState` | [repository-postgres-classification](../../rules/framework/3-infra/axis-repository/repository-postgres-classification.yaml) |
| MySQL classification by `MySqlException.Number` (shared `MySqlTransientErrors`) | [repository-mysql-classification](../../rules/framework/3-infra/axis-repository/repository-mysql-classification.yaml) |

## See also

- `axis-migrations` — schema, DDL, `AxisTable`, the SQL dialects and the migration runners (the "apply" half); this skill covers only the runtime data-access surface that consumes the resulting tables.
- `axis-domain-modeling` — the Reader/Writer ports and `IUnitOfWork` the repository implements.
- `axis-result` — the monad every method returns, and the railway `InTransactionAsync` composes on.
- `axis-mediator` — the ambient `CancellationToken` the base threads into every command.
- `axis-integration-tests` — how the repository is tested against a real Postgres/MySQL via Testcontainers.
- `axis-rules` — how these rules are authored and maintained (the extraction method from code).
