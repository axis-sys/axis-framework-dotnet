# Getting started · installation and usage

> Install the abstraction and the Postgres adapter, register a connection, write a small repository, and run your first transactional pipeline in five minutes.

---

## Installation

```
dotnet add package AxisRepository                 # the abstraction
dotnet add package AxisRepository.Postgres        # the Postgres adapter
```

`AxisRepository` depends on `AxisResult`. `AxisRepository.Postgres` adds `Npgsql` and depends on `AxisLogger` and `AxisTelemetry` (every database operation is traced).

---

## Registering Postgres

```csharp
using AxisRepository.Postgres;

builder.Services
    .AddAxisMediator()
    .AddAxisLogger()
    .AddAxisTelemetry()
    .AddPostgresUnitOfWork(
        serviceKey:       "main",
        connectionString: builder.Configuration.GetConnectionString("Postgres")!);

builder.Services.AddPostgresDbRepository(serviceKey: "main");
```

`AddPostgresUnitOfWork`:

- Calls `services.AddNpgsqlDataSource(...)` with a singleton data source and scoped connections.
- Registers a **keyed** `PostgresUnitOfWorkProvider`.
- Binds `IAxisUnitOfWork` and `IPostgresUnitOfWork` as keyed-scoped against the provider.

`AddPostgresDbRepository` registers the ready-made `PostgresDbRepository` executor as the scoped `IAxisDbRepository`, bound to the unit of work of that key — this is what your repositories compose.

The keyed registration lets you wire **multiple databases** under different `serviceKey` values — see [Keyed multi-database](keyed-multi-database.md).

---

## Writing a repository

```csharp
using Axis;

public class PersonRepository(IAxisDbRepository db)
{
    public Task<AxisResult> CreateAsync(Person person)
        => db.ExecuteAsync(
            sql: "INSERT INTO people (id, document, name) VALUES (@id, @doc, @name)",
            bind: b => b.Add("id",   person.PersonId)
                        .Add("doc",  person.Document)
                        .Add("name", person.DisplayName),
            duplicateKeyCode: "PERSON_DOCUMENT_ALREADY_EXISTS");

    public Task<AxisResult<Person>> GetByIdAsync(AxisEntityId personId)
        => db.GetAsync(
            sql: "SELECT id, document, name FROM people WHERE id = @id",
            bind: b => b.Add("id", personId),
            map: r => new Person(r.GetString(0), r.GetString(1), r.GetString(2)),
            notFoundCode: "PERSON_NOT_FOUND");
}
```

The repository **composes** the `IAxisDbRepository` executor — no driver type appears in the class, so the same repository runs on Postgres or MySQL by swapping only the registration. The executor methods (`ExecuteAsync`, `ExecuteCountAsync`, `GetAsync`, `ListAsync`) wrap parameter binding, retries on transient errors, and convert unique-key violations into `AxisError.Conflict(...)`.

> A MySQL adapter ships too — `AxisRepository.MySql`, registered with `AddMySqlUnitOfWork` + `AddMySqlDbRepository`. Same shape, `MySqlConnector` underneath. See [MySQL adapter](mysql-adapter.md). And if a repository is deliberately dialect-specific, the inheritance surface (`PostgresRepositoryBase`/`MySqlRepositoryBase`) remains available — see [Repository base](repository-base.md).

---

## Running inside a transaction

```csharp
public Task<AxisResult<CreatePersonResponse>> HandleAsync(CreatePersonCommand cmd)
    => uow.InTransactionAsync(() =>
        factory.CreateAsync(cmd)
            .ThenAsync(person => writer.CreateAsync(person))
            .MapAsync(_ => new CreatePersonResponse { PersonId = cmd.PersonId }));
```

**Why it pays off:** `InTransactionAsync` reads the railway. A failed step rolls back, a successful one commits, an exception rolls back and rethrows. The handler reads as a straight-line description of the use case.

---

## See also

- [The `IAxisUnitOfWork` contract](iaxisunitofwork.md) — the three primitives
- [`InTransactionAsync`](in-transaction.md) — the wrapper
- [Postgres adapter](postgres-adapter.md) — what `AddPostgresUnitOfWork` registers
- [MySQL adapter](mysql-adapter.md) — the bundled MySQL alternative
- [Repository base](repository-base.md) — the `ExecuteAsync`/`GetAsync`/`ListAsync` helpers
- [Schema DDL](ddl.md) — declare a table once, dialect-agnostic
- [Migrations](migrations.md) — apply the DDL to a schema, idempotently
- [Keyed multi-database](keyed-multi-database.md) — running against more than one database
- [Custom adapter](custom-adapter.md) — implement `IAxisUnitOfWork` for another store
- [Why AxisRepository?](why-axisrepository.md) — the case against EF Core
- [API reference](api-reference.md) — every member in one place

---

↩ [Back to AxisRepository docs](README.md)
