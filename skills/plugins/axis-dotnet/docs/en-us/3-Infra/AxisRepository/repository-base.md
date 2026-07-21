# Repository base · `AxisRepositoryBase<TCommand,TReader,TParameters>`

> The shared machinery behind every SQL repository — parameter binding, transparent retry on transient errors, unique-key violations turned into `AxisError.Conflict(...)`. It is consumed through **two surfaces**: an application repository **composes** the ready-made `IAxisDbRepository` executor (`PostgresDbRepository`/`MySqlDbRepository`) — the default —, and `PostgresRepositoryBase`/`MySqlRepositoryBase` remain available as the **inheritance** surface for a repository that is deliberately dialect-specific. Both funnel into the same private cores, so classification, retry and faulting behave identically.

```csharp
public class PersonRepository(IAxisDbRepository db)
{
    public Task<AxisResult> CreateAsync(Person person)
        => db.ExecuteAsync(
            sql: "INSERT INTO people (id, document, name) VALUES (@id, @doc, @name)",
            bind: b => b.Add("id",   person.PersonId)
                        .Add("doc",  person.Document)
                        .Add("name", person.DisplayName),
            duplicateKeyCode: "PERSON_DOCUMENT_ALREADY_EXISTS");
}
```

---

## The two surfaces

**Composition (the default).** The repository is provider-agnostic: it takes `IAxisDbRepository` by constructor and speaks the common ADO.NET surface — a named-parameter binder (`IDbParamBinder`) and `DbDataReader`. The concrete executor is registered once in the composition root (`services.AddPostgresDbRepository("main")` or `services.AddMySqlDbRepository("main")`), so the same repository runs on Postgres or MySQL by swapping only the registration.

**Inheritance (dialect-specific).** A repository that deliberately targets one provider and wants its types — `AddWithValue` on the `NpgsqlParameterCollection`, the typed reader — inherits `PostgresRepositoryBase` (or `MySqlRepositoryBase`) and uses the same four helpers as protected methods:

```csharp
public class PersonRepository(
    IAxisMediator mediator,
    IAxisLogger<PersonRepository> logger,
    [FromKeyedServices("main")] IPostgresUnitOfWork uow)
    : PostgresRepositoryBase(mediator, logger, uow)
{
    public Task<AxisResult> CreateAsync(Person person)
        => ExecuteAsync(
            sql: "INSERT INTO people (id, document, name) VALUES (@id, @doc, @name)",
            addParams: p =>
            {
                p.AddWithValue("id",   person.PersonId);
                p.AddWithValue("doc",  person.Document);
                p.AddWithValue("name", person.DisplayName);
            },
            duplicateKeyCode: "PERSON_DOCUMENT_ALREADY_EXISTS");
}
```

The dialect skins supply four hooks to the base (`IsTransient`, `IsDuplicateKey`, `IsSchemaMissing`, `ErrorPrefix`) — that is how the `PostgresDbRepository`/`MySqlDbRepository` executors themselves are built inside.

## When to use

Compose `IAxisDbRepository` by default — it keeps the repository provider-neutral and is the shape the Axis scaffolds and conventions materialize. Inherit from `PostgresRepositoryBase`/`MySqlRepositoryBase` only when the repository is deliberately dialect-specific and wants the provider's types.

## When *not* to use

| You want to… | Use instead |
|---|---|
| issue a single one-off raw command from a handler | call `IPostgresUnitOfWork.NewCommandAsync(sql)` (or `IMySqlUnitOfWork`) directly |
| run the same repository on the other bundled database | swap the registration (`AddPostgresDbRepository` ↔ `AddMySqlDbRepository`) — the composed repository does not change |
| target a database with no bundled adapter | a new dialect skin over `AxisRepositoryBase<TCommand,TReader,TParameters>` |
| use an ORM | a [custom adapter](custom-adapter.md) wrapping it |

---

## The four methods

On the composition surface (`IAxisDbRepository`):

| Method | Signature | What it does |
|---|---|---|
| `ExecuteAsync` | `Task<AxisResult> ExecuteAsync(string sql, Action<IDbParamBinder> bind, string? duplicateKeyCode = null)` | `ExecuteNonQueryAsync` with retry; unique-key violation → `AxisError.Conflict(duplicateKeyCode)` |
| `ExecuteCountAsync` | `Task<AxisResult<int>> ExecuteCountAsync(string sql, Action<IDbParamBinder> bind, string? duplicateKeyCode = null)` | same as `ExecuteAsync`, but returns the rows-affected count |
| `GetAsync<T>` | `Task<AxisResult<T>> GetAsync<T>(string sql, Action<IDbParamBinder> bind, Func<DbDataReader, T> map, string notFoundCode)` | reads a single row; `notFoundCode` if no row matched |
| `ListAsync<T>` | `Task<AxisResult<IEnumerable<T>>> ListAsync<T>(string sql, Action<IDbParamBinder> bind, Func<DbDataReader, T> map)` | reads N rows |

On the inheritance surface the same four exist as protected provider-typed methods — `Action<TParameters>` and `Func<TReader, T>`, where `TParameters`/`TReader` are `NpgsqlParameterCollection`/`NpgsqlDataReader` for Postgres and `MySqlParameterCollection`/`MySqlDataReader` for MySQL — plus parameterless-`addParams` siblings (`ExecuteAsync(sql, duplicateKeyCode)`, `GetAsync(sql, map, notFoundCode)`, `ListAsync(sql, map)`) for queries with no parameters.

Whichever the surface, each method opens a fresh command through `uow.NewCommandAsync(sql)` (which attaches the current connection + transaction), invokes your binding callback to bind parameters, then runs the appropriate execution method with cancellation from `mediator.CancellationToken`.

Two more failure paths are handled centrally, before either dialect base sees the exception:

- **Schema not ready** — when `IsSchemaMissing` matches (e.g. Postgres `42P01`/`3F000`, MySQL `1146`/`1049`), the call returns a transient `AxisError.ServiceUnavailable("{PREFIX}_SCHEMA_NOT_READY")` with no error log — this is an *expected* state before migrations run, not a bug.
- **Faulted transaction** — once a command fails, `uow.MarkFaulted()` is called and every later call on the same unit of work short-circuits with `AxisError.InternalServerError("{PREFIX}_TRANSACTION_FAULTED")` instead of executing (some engines abort the whole transaction on any error, so retrying would just fail again).

---

## Transient retry

Reading `AxisRepositoryBase.WithRetryAsync` directly:

| Trigger | Wait between attempts | Attempts |
|---|---|---|
| the dialect's `IsTransient(exception)` hook returns `true` **and** the unit of work has no uncommitted write yet (`!uow.HasUncommittedWrites`) | 100 ms, 200 ms, 400 ms, 1000 ms | 5 total (the call + 4 retries) |

For Postgres, `IsTransient` matches `NpgsqlException.SqlState` in `40001` (serialization failure), `40P01` (deadlock), `08006` (connection failure), `08003` (connection does not exist), `08001` (unable to connect), `57P03` (cannot connect now). For MySQL, `MySqlTransientErrors.IsTransient` matches the equivalent deadlock/lock-wait-timeout/connection-blip conditions.

The `!uow.HasUncommittedWrites` gate matters: a transient error aborts the whole transaction, so retrying the same command in place would just hit a "transaction aborted" error — and if an earlier write in the same transaction already landed, retrying would silently lose it. With no write yet, the retry releases the connection (`uow.ReleaseConnectionAsync()`) and starts fresh; once a write has landed, the transient is surfaced instead so the caller can replay the entire unit of work (this is exactly what the saga resumer does on a fresh attempt).

Anything else is **not** retried — it goes straight to the catch block and turns into an `AxisError.InternalServerError(...)`, or, for unique-key/schema-missing exceptions, the mapped `Conflict`/`ServiceUnavailable` above.

---

## Real-world examples

### 1. Idempotent `INSERT` with conflict mapping

```csharp
public Task<AxisResult> CreateAsync(Person person)
    => db.ExecuteAsync(
        sql: "INSERT INTO people (id, document, name) VALUES (@id, @doc, @name)",
        bind: b => b.Add("id",   person.PersonId)
                    .Add("doc",  person.Document)
                    .Add("name", person.DisplayName),
        duplicateKeyCode: "PERSON_DOCUMENT_ALREADY_EXISTS");
```

If the `document` column has a unique constraint and the insert violates it, the caller sees `AxisError.Conflict("PERSON_DOCUMENT_ALREADY_EXISTS")` — a typed, predictable error code, not a string-matched exception.

### 2. Typed `GetAsync` with custom mapping

```csharp
public Task<AxisResult<Person>> GetByIdAsync(AxisEntityId personId)
    => db.GetAsync(
        sql: "SELECT id, document, name FROM people WHERE id = @id",
        bind: b => b.Add("id", personId),
        map: r => new Person(r.GetString(0), r.GetString(1), r.GetString(2)),
        notFoundCode: "PERSON_NOT_FOUND");
```

If no row matches, the result is `AxisError.NotFound("PERSON_NOT_FOUND")` — chain `.RequireNotFoundAsync(...)` from `AxisResult` for the "must not exist" guard pattern.

### 3. Paged `ListAsync`

```csharp
public Task<AxisResult<IEnumerable<Person>>> ListByTenantAsync(string tenantKey, int limit, int offset)
    => db.ListAsync(
        sql: "SELECT id, document, name FROM people WHERE tenant = @tenant ORDER BY id LIMIT @limit OFFSET @offset",
        bind: b => b.Add("tenant", tenantKey)
                    .Add("limit",  limit)
                    .Add("offset", offset),
        map: r => new Person(r.GetString(0), r.GetString(1), r.GetString(2)));
```

**Why it pays off:** every repository method reads like a (parameters → SQL → projection) tuple. The boilerplate that usually buries that intent — `using`, retry, `try/catch`, error mapping — lives once in the base.

---

## See also

- [Postgres adapter](postgres-adapter.md) — one dialect skin the base sits under
- [MySQL adapter](mysql-adapter.md) — the other dialect skin, same shared base
- [The `IAxisUnitOfWork` contract](iaxisunitofwork.md) — the transaction primitives
- [Schema DDL](ddl.md) — declares the tables these repositories read and write
- [Custom adapter](custom-adapter.md) — when you need a different base

---

↩ [Back to AxisRepository docs](README.md)
