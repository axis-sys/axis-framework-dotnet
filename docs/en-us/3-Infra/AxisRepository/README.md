# AxisRepository — Documentation

> 🌐 [Português (documentação navegável)](../../../pt-br/3-Infra/AxisRepository/README.md)

**A unit-of-work abstraction with `AxisResult` baked in** — `IAxisUnitOfWork` with three primitives (`StartAsync`, `SaveChangesAsync`, `RollbackAsync`) and a default-interface `InTransactionAsync(work)` that orchestrates them. The bundled `AxisRepository.Postgres` and `AxisRepository.MySql` adapters ship a typed command builder, retry on transient errors, conflict detection on unique-constraint violations, and a keyed-DI provider for multi-database setups.

```csharp
public Task<AxisResult<CreatePersonResponse>> HandleAsync(CreatePersonCommand cmd)
    => uow.InTransactionAsync(() =>
        factory.CreateAsync(cmd)
            .ThenAsync(person => writer.CreateAsync(person))
            .MapAsync(_ => new CreatePersonResponse { PersonId = cmd.PersonId }));
```

Use this page as a **map**: read the trunk below (~5 min) and jump straight to the detail of the group you need — without reading hundreds of lines.

---

## The trunk (read first)

### The interface in 60 seconds

```csharp
public interface IAxisUnitOfWork : IDisposable, IAsyncDisposable
{
    Task<AxisResult> StartAsync();
    Task<AxisResult> SaveChangesAsync();
    Task<AxisResult> RollbackAsync();

    Task<AxisResult>      InTransactionAsync(Func<Task<AxisResult>> work);
    Task<AxisResult<T>>   InTransactionAsync<T>(Func<Task<AxisResult<T>>> work);
}
```

`StartAsync` opens a transaction; `SaveChangesAsync` commits; `RollbackAsync` rolls back. The two `InTransactionAsync` defaults wrap the three primitives — start, run the work, commit on success, **roll back on failure or exception** (and re-throw the exception). → **[The `IAxisUnitOfWork` contract](iaxisunitofwork.md)**

### Why a `Result`-aware unit of work?

A failed business step inside a transaction usually means *roll back* — but a `try/catch` is not the right test, because failure here is a **value** (`AxisResult.IsFailure`), not an exception. `InTransactionAsync` reads the result and decides: commit on `Ok`, rollback on `Error`, rethrow on a raw exception. → **[`InTransactionAsync` — the right transaction wrapper](in-transaction.md)**

### Bundled adapters — PostgreSQL and MySQL

`AxisRepository.Postgres` ships:

- **`PostgresUnitOfWork`** — implements `IAxisUnitOfWork` via `NpgsqlConnection` + `NpgsqlTransaction`, traces every operation through `IAxisTelemetry`.
- **`IPostgresUnitOfWork`** — `IAxisUnitOfWork` + `NewCommandAsync(sql)` for raw SQL.
- **`PostgresDbRepository`** — the ready-made `IAxisDbRepository` executor your repositories **compose**: `ExecuteAsync`/`ExecuteCountAsync`/`GetAsync`/`ListAsync` with parameter binding, retries on transient `SqlState` codes, and unique-key violations converted into `AxisError.Conflict`.
- **`PostgresRepositoryBase`** — the machinery under the executor, also available as an inheritance surface for a repository deliberately bound to the dialect.

```csharp
services.AddPostgresUnitOfWork(serviceKey: "main", connectionString: "Host=…");
services.AddPostgresDbRepository(serviceKey: "main");
```

`AxisRepository.MySql` ships the same shape over `MySqlConnector` — `MySqlUnitOfWork`, `IMySqlUnitOfWork`, `MySqlDbRepository`, `MySqlRepositoryBase` — sharing the retry/fault machinery with Postgres through `AxisRepositoryBase`:

```csharp
services.AddMySqlUnitOfWork(serviceKey: "main", connectionString: "Server=…");
services.AddMySqlDbRepository(serviceKey: "main");
```

→ **[Postgres adapter](postgres-adapter.md)** · **[MySQL adapter](mysql-adapter.md)** · **[Repository base](repository-base.md)** · **[Keyed multi-database](keyed-multi-database.md)**

### Schema DDL and migrations

A table is declared **once**, dialect-agnostic, with the fluent `AxisTable` builder (`Axis.Ddl`); an injected `IAxisSqlDialect` renders it into concrete Postgres or MySQL DDL:

```csharp
public static AxisTable Define() => new AxisTable("AXIS_CACHE.CACHE_ENTRIES")
    .Column("CACHE_KEY", AxisDbType.Varchar(200), primaryKey: true)
    .Column("VALUE_JSON", AxisDbType.Json, notNull: true)
    .Index("IDX_CACHE_ENTRIES_EXPIRES_AT", "EXPIRES_AT");
```

`IAxisMigrationRunner` then applies the rendered script to a schema idempotently — bootstrapping the schema and a `MIGRATIONS` control table, serializing concurrent instances with a schema-scoped lock, and skipping versions already recorded.

→ **[Schema DDL](ddl.md)** · **[Migrations](migrations.md)**

### Installation

```
dotnet add package AxisRepository                 # the abstraction
dotnet add package AxisRepository.Postgres        # the Postgres adapter (Npgsql)
dotnet add package AxisRepository.MySql           # the MySQL adapter (MySqlConnector)
```

→ Full guide: **[Getting started](getting-started.md)**

---

## The map (jump to what you need)

| Group | You want to… | Detail |
|---|---|---|
| **Contract · `IAxisUnitOfWork`** | the three primitives + the wrapper | [iaxisunitofwork.md](iaxisunitofwork.md) |
| **`InTransactionAsync`** ⭐ | wrap a railway in a transaction | [in-transaction.md](in-transaction.md) |
| **Postgres · `IPostgresUnitOfWork`** | the bundled `NpgsqlCommand` builder | [postgres-adapter.md](postgres-adapter.md) |
| **MySQL · `IMySqlUnitOfWork`** | the bundled `MySqlCommand` builder | [mysql-adapter.md](mysql-adapter.md) |
| **Repository base** | the `IAxisDbRepository` executor your repositories compose, and the `PostgresRepositoryBase`/`MySqlRepositoryBase` machinery underneath | [repository-base.md](repository-base.md) |
| **Schema DDL · `AxisTable`** | declare a table once, dialect-agnostic | [ddl.md](ddl.md) |
| **Migrations · `IAxisMigrationRunner`** | apply DDL to a schema, idempotently | [migrations.md](migrations.md) |
| **Multi-database** | keyed DI for two or more databases | [keyed-multi-database.md](keyed-multi-database.md) |
| **Custom adapter** | implement `IAxisUnitOfWork` for another store | [custom-adapter.md](custom-adapter.md) |
| **Why?** | the case against EF Core's `DbContext` | [why-axisrepository.md](why-axisrepository.md) |
| **Reference** | every member at a glance | [api-reference.md](api-reference.md) |

**Start here:** [Getting started](getting-started.md) · [The `IAxisUnitOfWork` contract](iaxisunitofwork.md) · [Why AxisRepository?](why-axisrepository.md)

**Fundamentals:** [`InTransactionAsync`](in-transaction.md) · [Postgres adapter](postgres-adapter.md) · [MySQL adapter](mysql-adapter.md) · [Repository base](repository-base.md)

**Reference & extras:** [Schema DDL](ddl.md) · [Migrations](migrations.md) · [Keyed multi-database](keyed-multi-database.md) · [Custom adapter](custom-adapter.md) · [API reference](api-reference.md)

---

## Design principles

1. **Transactions follow the rail.** A failed `AxisResult` rolls back; a successful one commits; an exception rolls back and rethrows.
2. **Errors are typed.** Unique-key violations → `Conflict`. Connection failures → `InternalServerError`. NotFound → `NotFound`. No string-matching at call sites.
3. **Transient errors retry transparently.** The repository base retries on Postgres `SqlState` 40001 / 40P01 / 08006 / 08003 / 08001 / 57P03 (and the equivalent MySQL deadlock/lock-wait-timeout/connection-blip conditions) with backoff.
4. **No `DbContext` smell.** Repositories speak SQL or ports, the unit of work owns the connection and the transaction. No proxies, no change tracking magic.
5. **Cancellation flows through `IAxisMediator`.** Every operation reads `mediator.CancellationToken` — no extra parameters in the contract.
6. **Schema is declared once, rendered per dialect.** An `AxisTable` is dialect-agnostic; `IAxisSqlDialect` renders it, `IAxisMigrationRunner` applies it idempotently. One definition can never drift between Postgres and MySQL because there is only one place to edit.

---

## License

Apache 2.0
