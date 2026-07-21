# API reference

> The complete catalogue, grouped by responsibility. Use it for lookup — each group links back to its detail page.

---

## The contract — `IAxisUnitOfWork`

| Method | Signature | Description |
|---|---|---|
| `StartAsync` | `Task<AxisResult> StartAsync()` | open the connection (if needed) and `BEGIN` the transaction |
| `SaveChangesAsync` | `Task<AxisResult> SaveChangesAsync()` | `COMMIT` and clear the transaction handle |
| `RollbackAsync` | `Task<AxisResult> RollbackAsync()` | `ROLLBACK`; no-op if no transaction is active |
| `ReleaseConnectionAsync` | `Task ReleaseConnectionAsync()` | rolls back any uncommitted work and returns the connection to the pool; no interface default — implementations without a pooled connection simply return `Task.CompletedTask` |
| `InTransactionAsync` | `Task<AxisResult> InTransactionAsync(Func<Task<AxisResult>> work)` | default-interface wrapper; commit on `Ok`, rollback on `Error`, rollback-then-rethrow on exception |
| `InTransactionAsync<T>` | `Task<AxisResult<T>> InTransactionAsync<T>(Func<Task<AxisResult<T>>> work)` | typed wrapper with the same semantics; on `Save` failure, returns the **save's** errors |
| `Dispose` | `void Dispose()` | disposes the underlying connection |
| `DisposeAsync` | `ValueTask DisposeAsync()` | async disposal |

→ [The `IAxisUnitOfWork` contract](iaxisunitofwork.md) · [`InTransactionAsync`](in-transaction.md)

---

## Postgres adapter — `AxisRepository.Postgres`

| Type | Description |
|---|---|
| `IPostgresUnitOfWork` | `IAxisUnitOfWork + Task<NpgsqlCommand> NewCommandAsync(string sql)` |
| `PostgresUnitOfWork` | the implementation: opens an `NpgsqlConnection`, manages `NpgsqlTransaction`, traces every primitive via `IAxisTelemetry` |
| `PostgresUnitOfWorkProvider` | caches a `PostgresUnitOfWork` per service key inside the scope |
| `PostgresDbRepository` | the ready-made `IAxisDbRepository` executor a provider-agnostic repository composes |
| `PostgresRepositoryBase` | the machinery under the executor (`ExecuteAsync`/`GetAsync`/`ListAsync` + retry); inheritance surface for dialect-specific repositories |

| Extension | Effect |
|---|---|
| `services.AddPostgresUnitOfWork(serviceKey, connectionString)` | registers a keyed `NpgsqlDataSource`, keyed `PostgresUnitOfWorkProvider`, keyed `IAxisUnitOfWork` and keyed `IPostgresUnitOfWork` |
| `services.AddPostgresDbRepository(serviceKey)` | registers `IAxisDbRepository` backed by `PostgresDbRepository` for that key |

→ [Postgres adapter](postgres-adapter.md) · [Keyed multi-database](keyed-multi-database.md)

---

## MySQL adapter — `AxisRepository.MySql`

| Type | Description |
|---|---|
| `IMySqlUnitOfWork` | `IAxisUnitOfWork + Task<MySqlCommand> NewCommandAsync(string sql)` (via `IDbUnitOfWork<MySqlCommand>`) |
| `MySqlUnitOfWork` | the implementation: opens a `MySqlConnection` from a `MySqlDataSource`, manages the `MySqlTransaction`, traces every primitive via `IAxisTelemetry` |
| `MySqlUnitOfWorkProvider` | caches a `MySqlUnitOfWork` per service key inside the scope |
| `MySqlDbRepository` | the ready-made `IAxisDbRepository` executor a provider-agnostic repository composes |
| `MySqlRepositoryBase` | the machinery under the executor (`ExecuteAsync`/`GetAsync`/`ListAsync` + retry), sharing `AxisRepositoryBase` with Postgres; inheritance surface for dialect-specific repositories |

| Extension | Effect |
|---|---|
| `services.AddMySqlUnitOfWork(serviceKey, connectionString)` | registers a keyed `MySqlDataSource`, keyed `MySqlUnitOfWorkProvider`, keyed `IAxisUnitOfWork` and keyed `IMySqlUnitOfWork` |
| `services.AddMySqlDbRepository(serviceKey)` | registers `IAxisDbRepository` backed by `MySqlDbRepository` for that key |

→ [MySQL adapter](mysql-adapter.md) · [Keyed multi-database](keyed-multi-database.md)

---

## The executor — `IAxisDbRepository`

| Method | Signature | Description |
|---|---|---|
| `ExecuteAsync` | `Task<AxisResult> ExecuteAsync(string sql, Action<IDbParamBinder> bind, string? duplicateKeyCode = null)` | `ExecuteNonQueryAsync` with retry; unique-key violation → `AxisError.Conflict(duplicateKeyCode)` |
| `ExecuteCountAsync` | `Task<AxisResult<int>> ExecuteCountAsync(string sql, Action<IDbParamBinder> bind, string? duplicateKeyCode = null)` | same as `ExecuteAsync`, but returns the rows-affected count |
| `GetAsync<T>` | `Task<AxisResult<T>> GetAsync<T>(string sql, Action<IDbParamBinder> bind, Func<DbDataReader, T> map, string notFoundCode)` | reads one row; missing → `AxisError.NotFound(notFoundCode)` |
| `ListAsync<T>` | `Task<AxisResult<IEnumerable<T>>> ListAsync<T>(string sql, Action<IDbParamBinder> bind, Func<DbDataReader, T> map)` | reads N rows |

Implemented by `PostgresDbRepository`/`MySqlDbRepository` (registered via `AddPostgresDbRepository`/`AddMySqlDbRepository`). On the inheritance surface (`PostgresRepositoryBase`/`MySqlRepositoryBase`), the same methods exist as protected provider-typed ones — `Action<TParameters>` and `Func<TReader, T>`, where `TParameters`/`TReader` are `NpgsqlParameterCollection`/`NpgsqlDataReader` for Postgres and `MySqlParameterCollection`/`MySqlDataReader` for MySQL.

→ [Repository base](repository-base.md)

---

## Schema DDL — `Axis.Ddl`

| Type | Description |
|---|---|
| `AxisTable` | fluent, dialect-agnostic table builder — `Column`/`Index`/`Unique`/`PartialIndex`/`PartialUnique`/`ForeignKey`/`Check`/`WithSeed`, each returning `this`; `Render(dialect)` produces the DDL string |
| `AxisColumn` | one column record — `Name`, `DbType`, `NotNull`, `Default`, `PrimaryKey`, `Check`, `Collation` |
| `AxisDbType` | logical column type — `Varchar(length)` / `Text` / `Int` / `Bool` / `Json` / `TimestampUtc` / `Decimal(precision, scale)` |
| `AxisDefault` | column default — `NowUtc` / `Bool(value)` / `Int(value)` / `Raw(sql)` |
| `AxisCheck` | column-level check — `IsTrue` (single-row-guard pattern) |
| `AxisCollation` | per-column collation intent — `Default` / `CaseAccentSensitive` / `CaseInsensitiveAccentSensitive` |
| `AxisIndex` | an index record — `Name`, `Columns`, `Unique`, `PartialPredicate` |
| `AxisForeignKey` | a table-level FK record — `Name`, `Column`, `ReferencedTable`, `ReferencedColumn`, `OnDeleteCascade` |
| `AxisTableCheck` | a table-level `CHECK` record — `Name`, `Expression` (portable SQL) |
| `AxisSeed` | an idempotent seed record — `Columns`, `ConflictColumns`, `Rows` |
| `IAxisSqlDialect` | renders the DDL model into one database's SQL — `RenderCreateTable(table)` for the full table, `RenderAddColumn(table, column)` for one portable `ALTER TABLE … ADD COLUMN` |
| `AxisSqlDialectBase` | shared rendering skeleton; nine abstract hooks (`RenderType`, `RenderDefault`, `RenderCheck`, `RenderCollation`, `RenderBoolLiteral`, `RenderSeedConflict`, `RenderInlineIndexLines`, `RenderPostTableStatements`, `RenderForeignKey`, `RenderTimestampLiteral`) plus shared helpers (`ForeignKeyConstraint`, `Quote`, `FormatUtcTimestamp`, `RenderNull`) |
| `PostgresSqlDialect` / `MySqlSqlDialect` | the two shipped `IAxisSqlDialect` implementations, one per adapter package |

→ [Schema DDL](ddl.md)

---

## Migrations — `IAxisMigrationRunner`

| Type | Description |
|---|---|
| `IAxisMigrationRunner` | `Task RunAsync(string connectionString, string schema, (string Version, string Script)[] migrations)` — bootstraps the schema + `MIGRATIONS` control table, applies pending versions in order, skipping recorded ones |
| `PostgresMigrationRunner` | the Postgres implementation — one transaction for the whole batch, transactional advisory lock (`pg_advisory_xact_lock`) |
| `MySqlMigrationRunner` | the MySQL implementation — no enclosing transaction (MySQL DDL implicit-commits), session named lock (`GET_LOCK`/`RELEASE_LOCK`), each version recorded immediately after it runs |

→ [Migrations](migrations.md)

---

## Transient codes the base retries

| Dialect | Codes | Meaning |
|---|---|---|
| Postgres `SqlState` | `40001` | serialization failure |
| Postgres `SqlState` | `40P01` | deadlock detected |
| Postgres `SqlState` | `08006` | connection failure |
| Postgres `SqlState` | `08003` | connection does not exist |
| Postgres `SqlState` | `08001` | unable to connect |
| Postgres `SqlState` | `57P03` | cannot connect now |
| MySQL `MySqlException.Number` | `1062` | duplicate entry → mapped to `Conflict`, not retried |
| MySQL `MySqlException.Number` | `1146` / `1049` | no such table / unknown database → mapped to `ServiceUnavailable`, not retried |
| MySQL (via `MySqlTransientErrors.IsTransient`) | deadlock / lock-wait-timeout / connection blips | retried |

Retry delays: `100`, `200`, `400`, `1000` ms (4 retries, 5 attempts total).

→ [Repository base](repository-base.md)

---

## Behaviour contract (for adapters)

| Outcome of `work()` (in `InTransactionAsync`) | Returned | Side effect |
|---|---|---|
| `Ok` and `SaveChangesAsync` ok | `Ok` (with value, in `<T>` overload) | committed |
| `Ok` and `SaveChangesAsync` fails | save's `Error(errors)` | nothing committed |
| `Error(errors)` | the work's `Error(errors)` | rolled back |
| throws | rethrows | rolled back, then rethrown |

→ [Custom adapter](custom-adapter.md)

---

## See also

- [Getting started](getting-started.md) — install, register, persist
- [Why AxisRepository?](why-axisrepository.md) — the case for the abstraction
- [Full documentation](README.md) — the map of the whole documentation

---

↩ [Back to AxisRepository docs](README.md)
