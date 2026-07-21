# Migrations · `IAxisMigrationRunner`

> Applies a Bounded Context's pending migrations to one schema, idempotently — the swappable infra port that pairs with [`IAxisSqlDialect`](ddl.md#the-dialect--iaxissqldialect-and-axissqldialectbase)'s "render" half. Each adapter provides its own implementation, owning its own bootstrap, concurrency lock and transaction semantics; a dialect-agnostic caller migrates any provider by swapping the injected runner together with the matching dialect.

```csharp
namespace AxisSaga.Postgres.Persistence;

public static class AxisSagaMigrations
{
    // The schema is declared ONCE in core (AxisSagaSchema); here it is rendered with the
    // Postgres dialect and applied by the framework runner.
    public static Task InitializePostgresAsync(string connectionString)
        => new PostgresMigrationRunner().RunAsync(
            connectionString,
            AxisSagaSchema.Schema,
            AxisSagaSchema.Migrations(new PostgresSqlDialect()));
}
```

---

## When to use

Any package or Bounded Context that owns a Postgres or MySQL schema and needs it created and evolved on startup — idempotently, and safely under multiple instances migrating the same schema at once. Pair it with [`AxisTable`](ddl.md) for the DDL itself.

## When *not* to use

| You want to… | Use instead |
|---|---|
| declare the table shape (columns, indexes, FKs) | [`AxisTable`](ddl.md) — this page only **applies** an already-rendered script |
| use an ORM's migration tool (EF Core Migrations, Flyway, Liquibase) | not supported — write the DDL with `AxisTable` (or raw SQL) and apply it with `IAxisMigrationRunner`; `AxisRepository` never wraps an ORM |
| target a database with no `IAxisMigrationRunner` yet | implement one, paired with a new [`IAxisSqlDialect`](ddl.md#the-dialect--iaxissqldialect-and-axissqldialectbase) for that database |

---

## The contract

```csharp
public interface IAxisMigrationRunner
{
    Task RunAsync(string connectionString, string schema, (string Version, string Script)[] migrations);
}
```

One method. `migrations` is an ordered array of `(Version, Script)` tuples — typically `{Package}Schema.Migrations(dialect)` or `{BC}DbInit.Migrations`, each `Script` produced by [`AxisTable.Render(dialect)`](ddl.md) or a raw SQL string.

## What `RunAsync` does, step by step

1. **Bootstrap idempotently, outside any transaction** — `CREATE SCHEMA IF NOT EXISTS {schema}`, then `CREATE TABLE IF NOT EXISTS {schema}.MIGRATIONS (VERSION VARCHAR(50) PRIMARY KEY, APPLIED_AT ... NOT NULL DEFAULT ...)`.
2. **Acquire a lock scoped to the schema name** — so two instances migrating the same schema at the same time serialize instead of racing.
3. **For each `(Version, Script)`, in array order** — skip if `VERSION` is already in `MIGRATIONS`; otherwise execute `Script`, then insert the version.
4. **Release the lock.**

Both shipped runners follow these four steps; the lock mechanics and the transaction boundary around step 3 are where they diverge — see below.

---

## Postgres vs MySQL — two different concurrency and atomicity models

| | `PostgresMigrationRunner` | `MySqlMigrationRunner` |
|---|---|---|
| **Concurrency lock** | transactional advisory lock — `SELECT pg_advisory_xact_lock(hashtext(schema))` as the transaction's first statement; released automatically on commit or rollback | session-scoped named lock — `SELECT GET_LOCK(schema, 30)`; explicitly released with `RELEASE_LOCK` in a `finally`, so it's freed even if a script throws |
| **Transaction scope** | the **entire pending batch** runs inside one transaction; any script failing rolls back every migration attempted in that run | **no enclosing transaction** — MySQL DDL causes an implicit commit and cannot be rolled back. Each version is recorded in `MIGRATIONS` immediately after its own script succeeds, so a failure mid-batch leaves the already-applied versions recorded; a re-run resumes from the first pending one instead of re-attempting what already landed |
| **Bootstrap connection** | opens directly against the connection string's database | connects at the **server level** first (`Database` cleared on the builder) — MySQL refuses a connection whose default database doesn't exist yet (`ERROR 1049`), so the runner creates the connection's own database (when one is named) and the target schema from a database-less connection before anything else runs |
| **`MIGRATIONS.APPLIED_AT`** | `TIMESTAMPTZ NOT NULL DEFAULT NOW()` | `DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)` |
| **Lock acquisition failure** | blocks until the lock is available (transactional lock semantics) | throws `InvalidOperationException` if the lock isn't acquired within 30 seconds |

Both runners are `sealed class` implementations of `IAxisMigrationRunner`, one per adapter package (`AxisRepository.Postgres` / `AxisRepository.MySql`) — swap the runner and the matching dialect together to migrate a different database; the caller (`{Package}Schema.Migrations`, `{BC}DbInit`) never changes.

---

## Idempotency — safe to call on every startup

`MIGRATIONS.VERSION` is the primary key of the control table, so "already applied" is a row lookup, not a guess. Calling `InitializePostgresAsync`/`InitializeMySqlAsync` twice in a row is a no-op the second time — every version is already recorded, so `RunAsync` bootstraps (both `CREATE ... IF NOT EXISTS`, so also a no-op), takes the lock, finds nothing pending, and returns. That is exactly what lets a hosted worker or a test fixture call the initializer unconditionally on every startup instead of tracking "did I already migrate?" itself.

**Never modify a version already shipped to production.** A recorded `VERSION` is never re-applied, even if you edit the `const string` behind it — a script rendered from a changed `AxisTable` under the same `"V1"` key silently never reaches a database that already has `V1` recorded. In development, editing `V1` directly is fine (nothing has run against it yet); in production, always append a new version instead:

```csharp
public static (string Version, string Script)[] Migrations(IAxisSqlDialect dialect) =>
[
    ("V1", /* the original AxisTable.Render(dialect) calls */),
    ("V2", /* an additional table, or raw ALTER/INSERT SQL for the new one */),
];
```

For the most common evolution — a new column on an existing table — [`IAxisSqlDialect.RenderAddColumn`](ddl.md#adding-a-column--renderaddcolumn) renders the portable `ALTER TABLE … ADD COLUMN` for the new version's script, so `V2` needs no hand-written engine tokens.

---

## Real-world examples

### 1. Two adapters, one schema — `AxisSaga`

```csharp
// AxisSaga.Postgres
public static class AxisSagaMigrations
{
    public static Task InitializePostgresAsync(string connectionString)
        => new PostgresMigrationRunner().RunAsync(
            connectionString, AxisSagaSchema.Schema, AxisSagaSchema.Migrations(new PostgresSqlDialect()));
}

// AxisSaga.MySql
public static class AxisSagaMySqlMigrations
{
    public static Task InitializeMySqlAsync(string connectionString)
        => new MySqlMigrationRunner().RunAsync(
            connectionString, AxisSagaSchema.Schema, AxisSagaSchema.Migrations(new MySqlSqlDialect()));
}
```

Both call into `AxisSagaSchema.Migrations(dialect)`, which renders the same four `AxisTable` definitions — `SagaInstancesTable` first, since `SagaStageLogsTable` foreign-keys it — into a single consolidated `"V1"`. Each storage adapter's startup initializer (`PostgresSagaStorageInitializer.InitializeAsync` / `MySqlSagaStorageInitializer.InitializeAsync`) calls its dialect's `InitializeXAsync`, and the built-in resumer worker calls that initializer on startup — idempotent, so it is a no-op when a prior run (or a test fixture) already migrated the schema. It never destroys data.

**Why it pays off:** the schema is defined once; picking Postgres vs MySQL for a deployment is a one-line adapter swap, not two DDL scripts to keep in sync.

### 2. A single-table package schema — `AxisCache`

```csharp
public static class AxisCacheSchema
{
    public const string Schema = CacheEntriesTable.Schema;

    public static (string Version, string Script)[] Migrations(IAxisSqlDialect dialect) =>
        [("V1", CacheEntriesTable.Define().Render(dialect))];
}
```

An adapter's initializer runs `new PostgresMigrationRunner().RunAsync(connectionString, AxisCacheSchema.Schema, AxisCacheSchema.Migrations(new PostgresSqlDialect()))` (or the MySQL equivalent) the same way — the pattern does not change with the number of tables.

**Why it pays off:** a one-table package pays exactly the same wiring cost as a four-table one — there is no extra ceremony to scale down to.

---

## See also

- [Schema DDL · `AxisTable`](ddl.md) — declares what `RunAsync` applies
- [Postgres adapter](postgres-adapter.md) — ships `PostgresMigrationRunner` and `PostgresSqlDialect`
- [MySQL adapter](mysql-adapter.md) — ships `MySqlMigrationRunner` and `MySqlSqlDialect`
- [Database schema · `AXIS_SAGA`](../../2-ApplicationFlow/AxisSaga/database-schema.md) — the full real-world schema these examples reference

---

↩ [Back to AxisRepository docs](README.md)
