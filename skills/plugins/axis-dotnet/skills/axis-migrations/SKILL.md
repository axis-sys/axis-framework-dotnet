---
name: axis-migrations
description: >
  Evolve schema/DDL and apply idempotent, versioned migrations on Axis — the DDL subsystem that ships inside
  AxisRepository but is a distinct concern from runtime data access. Use when creating a new schema, adding a
  table to a feature, or evolving one with a new version. Declare a table ONCE as a dialect-agnostic
  `AxisTable`, let an `IAxisSqlDialect` render Postgres or MySQL DDL, and hand the `(Version, Script)[]` batch
  to the framework `IAxisMigrationRunner`, which bootstraps the schema, serializes concurrent runners with a
  lock, applies each pending version in order and records it in `{SCHEMA}.MIGRATIONS` — never re-applying a
  recorded version. This skill is a MAP: each row points at the canonical rule in `rules/`; open only the one
  the context asks for. It does NOT restate invariants nor carry code, and it does NOT cover the runtime
  repository/unit-of-work side (→ axis-repository) nor the monad every operation composes with (→ axis-result).
---

# AxisMigrations — rule map (schema DDL & migration runner)

The migration subsystem lives physically inside **AxisRepository** but is a **distinct concern**: schema,
DDL and idempotent versioned migration, not query execution. It has two swappable halves — the **render**
half (`IAxisSqlDialect` turns a dialect-agnostic `AxisTable` into one engine's DDL) and the **apply** half
(`IAxisMigrationRunner` bootstraps the schema plus its `MIGRATIONS` ledger and applies the pending
`(Version, Script)[]` batch). A Bounded Context declares its schema **once** and targets Postgres or MySQL by
swapping the injected dialect and runner. Everything is **pure SQL** — no ORM, no external migration tool.

This skill **does not restate** the invariants nor carry code — it **routes**. Each map row points at the
canonical rule (in English) under `rules/framework/3-infra/axis-migrations/`; open **only** the rule the
context requires.

## Rule map

### Start here — route by intent ⭐

| Context / what you were about to write | Rule |
|---|---|
| Declare a table once, dialect-agnostic, render per dialect | [migrations-table-single-declaration](../../rules/framework/3-infra/axis-migrations/migrations-table-single-declaration.yaml) |
| Apply the pending versions — bootstrap, lock, record, never re-apply | [migrations-runner-apply-port](../../rules/framework/3-infra/axis-migrations/migrations-runner-apply-port.yaml) |
| Migrations are plain SQL — no ORM, no external migration tool | [migrations-pure-sql-no-orm](../../rules/framework/3-infra/axis-migrations/migrations-pure-sql-no-orm.yaml) |

### The DDL model — `Axis.Ddl.*`

| Context | Rule |
|---|---|
| Column types — closed logical `AxisDbType`, mapped per dialect | [migrations-logical-db-types](../../rules/framework/3-infra/axis-migrations/migrations-logical-db-types.yaml) |
| Column defaults — closed `AxisDefault`, portable `NowUtc`, `Raw` escape hatch | [migrations-column-defaults](../../rules/framework/3-infra/axis-migrations/migrations-column-defaults.yaml) |
| Collation — declare the comparison intent so MySQL matches Postgres | [migrations-collation-intent](../../rules/framework/3-infra/axis-migrations/migrations-collation-intent.yaml) |
| Column `CHECK` — the closed `IsTrue` single-row boolean guard | [migrations-column-check-is-true](../../rules/framework/3-infra/axis-migrations/migrations-column-check-is-true.yaml) |
| Table-level `CHECK` — a raw portable expression rendered everywhere the same | [migrations-table-check-portable](../../rules/framework/3-infra/axis-migrations/migrations-table-check-portable.yaml) |
| Foreign key — one table-level named `CONSTRAINT`, optional cascade | [migrations-foreign-key-uniform](../../rules/framework/3-infra/axis-migrations/migrations-foreign-key-uniform.yaml) |
| The four index builders — uniqueness × optional partial predicate | [migrations-index-model-forms](../../rules/framework/3-infra/axis-migrations/migrations-index-model-forms.yaml) |
| Idempotent seed — no-op on conflict (NOT `INSERT IGNORE`) | [migrations-idempotent-seed](../../rules/framework/3-infra/axis-migrations/migrations-idempotent-seed.yaml) |

### Rendering — `IAxisSqlDialect` & `AxisSqlDialectBase`

| Context | Rule |
|---|---|
| The render port — owns type mapping AND layout; swap it to retarget | [migrations-dialect-render-port](../../rules/framework/3-infra/axis-migrations/migrations-dialect-render-port.yaml) |
| Evolve a table with a new column — one portable `ALTER TABLE ADD COLUMN` | [migrations-add-column-render](../../rules/framework/3-infra/axis-migrations/migrations-add-column-render.yaml) |
| The shared skeleton — `CREATE TABLE IF NOT EXISTS`, forced dialect tokens | [migrations-dialect-base-skeleton](../../rules/framework/3-infra/axis-migrations/migrations-dialect-base-skeleton.yaml) |
| Column-line token order; `PRIMARY KEY` wins over `NOT NULL` | [migrations-column-line-order](../../rules/framework/3-infra/axis-migrations/migrations-column-line-order.yaml) |
| Seed value rendering — numerics unquoted & invariant, strings escaped | [migrations-value-rendering-invariant](../../rules/framework/3-infra/axis-migrations/migrations-value-rendering-invariant.yaml) |
| Timestamps normalized to UTC before rendering (`Unspecified` → UTC) | [migrations-utc-timestamp-normalization](../../rules/framework/3-infra/axis-migrations/migrations-utc-timestamp-normalization.yaml) |
| Postgres tokens — BOOLEAN/JSONB/TIMESTAMPTZ, standalone indexes, offset-pinned | [migrations-postgres-rendering](../../rules/framework/3-infra/axis-migrations/migrations-postgres-rendering.yaml) |
| MySQL tokens — TINYINT(1)/JSON/DATETIME(6), inline indexes, offset-free | [migrations-mysql-rendering](../../rules/framework/3-infra/axis-migrations/migrations-mysql-rendering.yaml) |
| MySQL partial `UNIQUE` — emulated with a STORED generated key column | [migrations-mysql-partial-unique-emulation](../../rules/framework/3-infra/axis-migrations/migrations-mysql-partial-unique-emulation.yaml) |

### The runner, bootstrap & `MIGRATIONS` ledger — `IAxisMigrationRunner`

| Context | Rule |
|---|---|
| The apply port — one `RunAsync`, instantiated directly (not DI-registered) | [migrations-runner-apply-port](../../rules/framework/3-infra/axis-migrations/migrations-runner-apply-port.yaml) |
| Never re-apply a recorded version; record each on success; re-run is safe | [migrations-versioned-idempotent-apply](../../rules/framework/3-infra/axis-migrations/migrations-versioned-idempotent-apply.yaml) |
| Idempotent bootstrap of the schema and the `{SCHEMA}.MIGRATIONS` ledger | [migrations-idempotent-bootstrap-ledger](../../rules/framework/3-infra/axis-migrations/migrations-idempotent-bootstrap-ledger.yaml) |
| Postgres — one transaction for the batch, transactional advisory lock | [migrations-postgres-single-transaction-advisory-lock](../../rules/framework/3-infra/axis-migrations/migrations-postgres-single-transaction-advisory-lock.yaml) |
| MySQL — per-version commit (no rollback), session named lock | [migrations-mysql-per-version-commit-named-lock](../../rules/framework/3-infra/axis-migrations/migrations-mysql-per-version-commit-named-lock.yaml) |
| MySQL — server-level connect to bootstrap a bare server (ERROR 1049) | [migrations-mysql-bare-server-bootstrap](../../rules/framework/3-infra/axis-migrations/migrations-mysql-bare-server-bootstrap.yaml) |

## See also

- `axis-repository` — the runtime data-access side of AxisRepository (`IAxisDbRepository`, `{Entity}DbEntity`, the keyed unit of work) that CONSUMES the schema this subsystem creates.
- `axis-cache` — the two-tier SQL cache whose L2 schema is declared once and applied through this exact runner; a worked example of the versioned-batch pattern.
- `axis-result` — the monad the repository layer returns; the runner's `RunAsync` is a bootstrap `Task`, not an `AxisResult` (it runs before the app's railway starts).
- `axis-dotnet-architect` — the hub; where a schema declaration is wired into the host and the project rules for a new Bounded Context live.
