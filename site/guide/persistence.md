# Persistence

Axis persistence is hand-written, parameterized SQL executed through the `IAxisDbRepository` of
[AxisRepository](https://github.com/axis-sys/axis-repository). There is no ORM. Every operation returns
`AxisResult` and never throws; a use case stages its writes through ports and commits once through the single
`IUnitOfWork`; the schema is declared once as an `AxisTable` AST and rendered per provider. The result is one
repository layer that runs unchanged on Postgres ([AxisRepository.Postgres](https://github.com/axis-sys/axis-repository-postgres))
and MySQL ([AxisRepository.MySql](https://github.com/axis-sys/axis-repository-mysql)).

The aggregates these ports serve are described in [Domain](domain.md); the projects in
[Architecture](architecture.md); the dual-database test fixtures in [Testing](testing.md).

| Section | What it settles |
|---|---|
| [The persistence stack](#the-persistence-stack) | which project holds what |
| [Reader and Write ports](#reader-and-write-ports) | the contracts a repository implements |
| [Repositories](#repositories) | composition over `IAxisDbRepository`, and the per-entity dialect seam |
| [Row mapping](#row-mapping) | `{Entity}DbEntity` and its ordinal `FromReader` |
| [Columns and tables](#columns-and-tables) | the single source of every identifier and projection |
| [Parameterized SQL](#parameterized-sql) | no ORM, every runtime value bound |
| [Naming](#naming) | UPPER_SNAKE_CASE identifiers, one schema per bounded context |
| [Schema and migrations](#schema-and-migrations) | `AxisTable.Define()`, `{BC}DbInit`, the migrator and when it runs |
| [Dependency injection](#dependency-injection) | the registration levels, from bounded context to host, and the keyed unit of work |
| [Unit of work](#unit-of-work) | one `SaveChangesAsync` per use case, `RenewAsync` before a locked read |
| [Errors, not exceptions](#errors-not-exceptions) | no `try/catch` in repositories; codes from `{Aggregate}Errors` |
| [Connection pools](#connection-pools) | business and saga pools, sized and budgeted |
| [Multiple providers](#multiple-providers) | parity when an application targets more than one engine |
| [Concurrency traps](#concurrency-traps) | patterns that stay correct on every engine |
| [Tenant-scoped queries](#tenant-scoped-queries) | the tenant filter in every statement |
| [Checklist: a new table](#checklist-a-new-table) | the files to add, in order |

---

## The persistence stack

| Layer | Project | Holds |
|---|---|---|
| Contracts | `{App}.Contracts.Driven` | `I{Entities}ReaderPort`, `I{Entities}WritePort`, `{Aggregate}Errors`, the single `IUnitOfWork` |
| Provider-agnostic adapter | `{App}.Adapters.Driven.Repository` | repositories, `{Entity}DbEntity`, `{Entities}Columns`, `{Entities}Table`, `{BC}DbInit`, the per-entity dialect interfaces, the `IUnitOfWork` adapter, the migrator adapter |
| Provider adapters | `{App}.Adapters.Driven.Postgres`, `{App}.Adapters.Driven.MySql` | provider wiring and dialect implementations, nothing else |
| Host | `{App}.Host` | the provider switch, the pool sizing, the `migrate` verb |

Each project is foldered by bounded context, then aggregate:
`Catalog/Products/IProductsWritePort.cs` in the contracts, `Catalog/Products/ProductsRepository.cs` in the
repository adapter.

---

## Reader and Write ports

Ports live in `{App}.Contracts.Driven`, mirrored on the domain folder tree, with the aggregate's errors class
beside them. Reads and writes are separate interfaces ([`IProductsReaderPort`][iproducts-reader],
[`IProductsWritePort`][iproducts-write]):

```csharp
public interface IProductsReaderPort
{
    Task<AxisResult<IProductEntityProperties>> GetByIdAsync(ProductId productId);
    Task<AxisResult<IProductEntityProperties>> GetBySkuAsync(Sku sku);
}

public interface IProductsWritePort
{
    Task<AxisResult<IProductEntityProperties>> CreateAsync(IProductEntityProperties product);
    Task<AxisResult<IProductEntityProperties>> GetByIdForUpdateAsync(ProductId productId);
    Task<AxisResult> ReserveStockAsync(ProductId productId, int quantity);
}
```

| Convention | Detail |
|---|---|
| Naming | `I{Entities}ReaderPort` and `I{Entities}WritePort`: "Reader" but "Write" is the established asymmetry. Never `WriterPort`. |
| Return type | `Task<AxisResult>` or `Task<AxisResult<T>>`, where `T` is the domain `I{Entity}EntityProperties`, a sequence of it, a scalar or a read model; never a concrete record or a `DbEntity`. Nothing throws. |
| Creation | takes the property interface, `CreateAsync(I{Entity}EntityProperties properties)`; a child collection gets an add method |
| Targeted mutation | value-object ids plus scalars, `ReserveStockAsync(ProductId productId, int quantity)` |
| Ids and foreign keys | shared-kernel value objects, never raw strings or `Guid`s; a method scoped to a key (a tenant, a parent) takes it as the first parameter |
| Paging | `(..., int page, int pageSize)`, paired with a `Count` variant |
| Commit | no `Save`, `Commit` or `Flush` on any port: committing belongs to the single [`IUnitOfWork`](#unit-of-work) |
| N0 lookup | a Reader port only; a Write port exists only when a use case mutates the aggregate |

In the reference application the locking read `GetByIdForUpdateAsync` is declared on the write port, next to the
write it precedes in the checkout.

**Exceptions.**

- A foreign or external identifier that has no value object (an external identity id, a license key, a status
  code string) may be passed as a raw string.
- Two reader ports for the same concept may coexist in different namespaces when each bounded context owns its
  own model of it.
- Filter and read-model helper records used by a search live beside the port in the same contract folder, not
  in the driving contracts.

---

## Repositories

A repository is a provider-agnostic `internal sealed` class that implements both ports of its aggregate and
**composes** `IAxisDbRepository` by constructor injection. It never inherits a provider base class, the package's
`AxisRepositoryBase` included: composition is what lets one class serve every provider.

`IAxisDbRepository` is the whole execution surface:

| Method | Runs | The code you pass |
|---|---|---|
| `ExecuteAsync(sql, bind, duplicateKeyCode)` | insert, update, delete | `duplicateKeyCode`: a unique violation becomes a `Conflict` with that code and rolls back to a savepoint, leaving the unit of work usable; without it, a duplicate faults the unit of work |
| `ExecuteCountAsync(sql, bind, duplicateKeyCode)` | a write whose affected-row count matters | the same |
| `GetAsync<T>(sql, bind, map, notFoundCode)` | a query mapped from its first row | `notFoundCode`: no row becomes a `NotFound` with that code |
| `ListAsync<T>(sql, bind, map)` | a query mapped from every row | |

SQL that differs between providers goes behind a **per-entity dialect seam**, `I{Entity}SqlDialect`, with
exactly one implementation per targeted provider; its members return SQL text only. The Members aggregate stores
its permissions as JSON, and each engine casts that parameter differently ([`IMemberSqlDialect`][imember-dialect],
[`PostgresMemberSqlDialect`][postgres-member-dialect], [`MySqlMemberSqlDialect`][mysql-member-dialect]):

```csharp
public interface IMemberSqlDialect
{
    string PermissionsParameter { get; }
}

internal sealed class PostgresMemberSqlDialect : IMemberSqlDialect
{
    public string PermissionsParameter => "CAST(@permissions AS JSONB)";
}

internal sealed class MySqlMemberSqlDialect : IMemberSqlDialect
{
    public string PermissionsParameter => "CAST(@permissions AS JSON)";
}
```

[`MembersRepository`][members-repository] stays provider-neutral by interpolating the dialect member:

```csharp
internal sealed class MembersRepository(IAxisDbRepository db, IMemberSqlDialect sqlDialect) : IMembersReaderPort, IMembersWritePort
{
    private const string Select = $"SELECT {MembersColumns.All}";

    public Task<AxisResult<IMemberEntityProperties>> CreateAsync(IMemberEntityProperties member)
        => db.ExecuteAsync(
                $"INSERT INTO {MembersTable.Table} ({MembersColumns.All}) VALUES (@customerId, @disabled, {sqlDialect.PermissionsParameter})",
                b => b.Add("customerId", member.CustomerId.ToString())
                    .Add("disabled", member.Disabled)
                    .AddJson("permissions", AxisJsonColumn.WriteStrings(member.Permissions)),
                duplicateKeyCode: MemberErrors.MemberAlreadyExists.Code)
            .WithValueAsync(member);
}
```

| Do | Don't |
|---|---|
| `internal sealed class ProductsRepository(IAxisDbRepository db)` | inherit a provider base class, or take `NpgsqlConnection` / `MySqlConnection` |
| put provider-divergent SQL behind `I{Entity}SqlDialect` | branch on the provider inside the repository |
| keep dialect members to SQL text | open connections, execute commands or touch the reader in a dialect |
| named parameters bound through the builder: `b.Add("productId", ...)` | positional placeholders or `AddWithValue` |
| mappers over the provider-neutral `DbDataReader` | mappers over `NpgsqlDataReader` or `MySqlDataReader` |
| project columns from `{Entities}Columns.All` | an inline column list |

A repository whose SQL is portable across every targeted provider takes no dialect at all:
[`ProductsRepository`][products-repository] injects only `IAxisDbRepository`. The repository project references
the domain project to consume the property interfaces, returns `AxisResult` from every method and never throws.

---

## Row mapping

Each entity has an `internal sealed record {Entity}DbEntity`, declared next to its repository, that implements the
domain's `I{Entity}EntityProperties` and maps a row in a static `FromReader(DbDataReader reader)`. It is the only
place where raw reader access happens ([`CustomerDbEntity`][customer-dbentity]):

```csharp
internal sealed record CustomerDbEntity(
    CustomerId CustomerId,
    string Email,
    string Name,
    bool IsAdmin,
    string? ExternalId,
    string? Provider,
    bool EmailValidated
) : ICustomerEntityProperties
{
    internal static CustomerDbEntity FromReader(DbDataReader reader)
        => new(
            (CustomerId)reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetBoolean(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.GetBoolean(6)
        );
}
```

- [ ] Read **by ordinal**, in exactly the order of `{Entities}Columns.All`; never by column name.
- [ ] Materialize a value object with an explicit cast from the raw value: `(CustomerId)reader.GetString(0)`.
- [ ] Guard a nullable column with `IsDBNull` first. Value-object parsing rejects null, so a nullable value
      object takes the lifted cast behind that guard and a null flows through as a typed null:
      `reader.IsDBNull(i) ? null : (CartId?)reader.GetString(i)`.
- [ ] Read a timestamp as a `DateTimeOffset` through the reader's typed getter (`GetFieldValue<DateTimeOffset>`),
      never as a local `DateTime`.
- [ ] Read a JSON list with `AxisJsonColumn.ReadStrings` ([`MemberDbEntity`][member-dbentity]).
- [ ] Pass `{Entity}DbEntity.FromReader` as the `map` argument of `GetAsync` and `ListAsync`. Scalar and aggregate
      projections (counts, grouped counts, single-column lists) map with inline lambdas instead of a `DbEntity`.

**Why.** The mapper takes the provider-neutral `DbDataReader`, so one `DbEntity` serves every provider. The
ordinal contract ties `FromReader` to `{Entities}Columns.All`: reordering one without the other silently mis-maps
rows, so they always change together.

---

## Columns and tables

Every table splits its metadata across two static classes in the same folder:

| Class | Holds |
|---|---|
| `{Entities}Columns` | one `public const string` per column, plus `All`, on **one line**, listing every column in the order `FromReader` reads them |
| `{Entities}Table` | the `Table` const, built from the bounded context's schema const and the table name, next to its `Define()` |

[`ProductsColumns`][products-columns]:

```csharp
public static class ProductsColumns
{
    public const string ProductId = "PRODUCT_ID";
    public const string Sku = "SKU";
    public const string Name = "NAME";
    public const string Stock = "STOCK";

    public const string All = $"{ProductId}, {Sku}, {Name}, {Stock}";
}
```

Beside its `Define()`, [`ProductsTable`][products-table] declares
`public const string Table = $"{CatalogDbInit.Schema}.PRODUCTS";`.

Repositories build SQL **only** by interpolating these consts. One `Select` const projects `All` and serves every
full-row query ([`ProductsRepository`][products-repository]):

```csharp
private const string Select = $"SELECT {ProductsColumns.All}";

public Task<AxisResult<IProductEntityProperties>> GetByIdAsync(ProductId productId)
    => db.GetAsync<IProductEntityProperties>(
        $"{Select} FROM {ProductsTable.Table} WHERE {ProductsColumns.ProductId} = @productId",
        b => b.Add("productId", productId.ToString()),
        ProductDbEntity.FromReader,
        ProductErrors.ProductNotFound.Code);
```

- `{Entities}Columns` never declares `Table`. The table has exactly one home, beside `Define()`, so the DDL and
  the runtime SQL reference the same const.
- No raw table or column literal appears in repository SQL, and a repository never reaches another table through
  that table's `Columns` class.
- Partial projections (`COUNT`, aggregates, joins picking specific columns) still name columns through the consts.
- The reference application also reuses `All` as the column list of every `INSERT`, so the `VALUES` follow the
  same order.

**Exception.** A seed-only lookup table that has no repository may omit `All`; its `Table` const still lives in
`{Entities}Table`.

---

## Parameterized SQL

Persistence is hand-written SQL run through `IAxisDbRepository` with a parameter-binder lambda. No ORM, micro-ORM
or query-builder package is referenced by the repository projects.

| Convention | In practice |
|---|---|
| Every runtime value is a bound parameter | `b => b.Add("sku", sku.ToString())`; a `null` value binds as `DBNull` |
| JSON goes through the binder's JSON add | `.AddJson("permissions", AxisJsonColumn.WriteStrings(member.Permissions))` |
| Interpolate only compile-time consts and dialect members | `{ProductsColumns.Sku}`, `{ProductsTable.Table}`, a domain code const, `{sqlDialect.PermissionsParameter}` (whose own parameters are still bound) |
| Value objects bind through their string form | `.Add("productId", product.ProductId.ToString())`; a nullable value object binds its string form or `null` |
| A variable-length `IN` list | one named parameter per element, each bound; an empty list degenerates to a constant predicate (a false or true literal), never to malformed SQL |
| A dynamic `WHERE` | the builder returns both the SQL fragment and its bindings, as data, so the repository binds them uniformly |

| Do | Don't |
|---|---|
| `$"{Select} FROM {ProductsTable.Table} WHERE {ProductsColumns.Sku} = @sku"` with `b.Add("sku", sku.ToString())` | `$"... WHERE SKU = '{sku}'"`: a caller-sourced value interpolated into SQL |

The repository writes SQL and supplies bindings; it never opens a connection itself.

---

## Naming

Every physical identifier follows one convention, so SQL reads the same on every provider:

| Identifier | Convention | Reference application |
|---|---|---|
| Schema | one per bounded context, UPPER_SNAKE_CASE, the `public const string Schema` of its `{BC}DbInit` | `ECOMMERCE_CATALOG`, `ECOMMERCE_CUSTOMERS`, `ECOMMERCE_EMAIL_VALIDATION`, `ECOMMERCE_AUTHORIZATION` |
| Table | plural UPPER_SNAKE_CASE; a relation table is named for the two sides it joins | `PRODUCTS`, `CART_ITEMS`, `VALIDATION_CODES` |
| Column | UPPER_SNAKE_CASE | `PRODUCT_ID`, `EMAIL_VALIDATED` ([`CustomersColumns`][customers-columns]) |
| Index | `IX_{TABLE}_{QUALIFIER}` | |
| Unique constraint | `UX_{TABLE}_{QUALIFIER}` | `UX_PRODUCTS_SKU`, `UX_CUSTOMERS_EMAIL` |
| Foreign key | `FK_{TABLE}_{TARGET}` | |
| SQL parameter | camelCase, mirroring the C# property | `@productId`, `@emailValidated` |
| Lookup and status primary keys | stable English domain codes, seeded from the DDL | |

Table names are unique per schema, not globally: two bounded contexts may each own a similarly named table under
their own schema.

---

## Schema and migrations

Schema is never written as raw provider SQL. Each table has a public static `{Entities}Table` whose `Define()`
builds an `AxisTable` AST over its own `Table` const; the injected `IAxisSqlDialect` renders it to each provider's
DDL at migration time, so one definition serves every engine ([`ProductsTable`][products-table]):

```csharp
public static AxisTable Define() => new AxisTable(Table)
    .Column(ProductsColumns.ProductId, AxisDbType.Varchar(50), primaryKey: true)
    .Column(ProductsColumns.Sku, AxisDbType.Varchar(100), notNull: true, collation: AxisCollation.CaseAccentSensitive)
    .Column(ProductsColumns.Name, AxisDbType.Varchar(200), notNull: true)
    .Column(ProductsColumns.Stock, AxisDbType.Int, notNull: true)
    .Unique("UX_PRODUCTS_SKU", ProductsColumns.Sku);
```

| The AST offers | For |
|---|---|
| `AxisDbType`: `Varchar(n)`, `Int`, `Bool`, `Json`, `Text`, `Decimal(precision, scale)`, `TimestampUtc` | column types |
| `AxisDefault`: `Bool(...)`, `Int(...)`, `NowUtc`, `Raw(...)` | defaults |
| `AxisCollation`: `CaseAccentSensitive`, `CaseInsensitiveAccentSensitive`, `Default` | comparison intent, wherever case or accent sensitivity matters |
| `Index`, `Unique`, `PartialIndex`, `PartialUnique`, `ForeignKey`, `PrimaryKey`, `Check` | indexes and constraints, naming columns through the `{Entities}Columns` consts only |
| `WithSeed(...)` | static lookup data, seeded declaratively inside `Define()` and idempotent |

Declaring the collation is what makes a unique key behave the same on both engines: `Sku` canonicalizes to upper
case ([Domain](domain.md#value-objects)) and `CaseAccentSensitive` makes Postgres and MySQL compare that canonical
form identically. No hand-written `CREATE TABLE` or `CREATE INDEX` exists anywhere in the repository projects.

### One `{BC}DbInit` per bounded context

Each bounded context owns a public static `{BC}DbInit` with its `Schema` const and a
`Migrations(IAxisSqlDialect)` returning ordered `(Version, Script)` pairs ([`CatalogDbInit`][catalog-dbinit]):

```csharp
public static class CatalogDbInit
{
    public const string Schema = "ECOMMERCE_CATALOG";

    private static AxisTable[] Tables =>
    [
        ProductsTable.Define(),
        OrdersTable.Define(),
        CartItemsTable.Define(),
    ];

    public static (string Version, string Script)[] Migrations(IAxisSqlDialect sqlDialect) =>
    [
        ("V1", string.Join("\n", Tables.Select(table => table.Render(sqlDialect)))),
    ];
}
```

- **V1 is consolidated.** It renders every table's `Define()` in foreign-key dependency order. While the
  application is unreleased, schema changes edit V1 in place; V2 and later appear only once a real environment
  exists, and a version applied in a real environment is never edited again.
- **One migrator adapter** implements the migrator port ([`IDatabaseMigratorPort`][imigrator-port]). It runs the
  bounded contexts in cross-context dependency order, one runner call per schema; resolves the connection string
  by name and returns a `ServiceUnavailable` (`AdminErrors.DatabaseConnectionStringMissing`) when it is missing;
  and wraps a failed run as an `InternalServerError` (`AdminErrors.DatabaseMigrationFailed`)
  ([`DatabaseMigratorAdapter`][migrator-adapter], [`AdminErrors`][admin-errors]):

  ```csharp
  private (string Schema, (string Version, string Script)[] Migrations)[] BoundedContexts =>
  [
      (CustomersDbInit.Schema, CustomersDbInit.Migrations(sqlDialect)),
      (CatalogDbInit.Schema, CatalogDbInit.Migrations(sqlDialect)),
      (EmailValidationDbInit.Schema, EmailValidationDbInit.Migrations(sqlDialect)),
      (AuthorizationDbInit.Schema, AuthorizationDbInit.Migrations(sqlDialect)),
  ];
  ```

- **The runner and the dialect are a matched pair, outside DI.** The provider entry point creates them once
  (`new PostgresSqlDialect()`, `new PostgresMigrationRunner()`) and hands them to the migrator adapter through its
  factory registration. Neither is registered in nor resolved from the container, so no resolution path can hand
  the adapter a runner of another provider ([`Repository/DependencyInjection.cs`][repository-di]):

  ```csharp
  services.AddScoped<IDatabaseMigratorPort>(provider =>
      ActivatorUtilities.CreateInstance<DatabaseMigratorAdapter>(provider, connectionStringName, sqlDialect, migrationRunner));
  ```

### When migrations run

On demand, through the migrator port, and never on the web host's startup path or inside DI registration:

| Trigger | Use it for | Reference application |
|---|---|---|
| the host's `migrate` verb: runs the migration use case and exits (0 on success, 1 on failure) | every deploy, and the first run against an empty database | [`Program.cs`][program], [`MigrateCommand`][migrate-command] |
| the admin endpoint `POST /api/v1/database-migrations` | later re-runs; the migration is idempotent | [`DatabaseMigrationsV1Controller`][migrations-controller] |

```csharp
var app = builder.Build();

if (MigrateCommand.IsRequested(args))
    return await MigrateCommand.RunAsync(app);
```

From the scaffold folder: `dotnet run --project Host/Scaffolds.ECommerce.Host -- migrate`. Both triggers dispatch
the same use case, [`MigrateDatabaseHandler`][migrate-handler]. The admin endpoint cannot bootstrap an empty
database: the token it requires is issued by writing a customer row, so the first migration of an environment
always goes through the verb.

**Exception.** Framework-owned schemas (the saga store, the bus outbox, the cache) are initialized by the
framework's own migration entry points, not by the application's migrator; the integration fixture
[`DualDbFixture`][dual-db-fixture] calls them after migrating through the port.

---

## Dependency injection

Wiring follows a fixed shape, from the bounded context up to the host:

| Level | Where | Registers |
|---|---|---|
| 1 · bounded context | an internal `Add{BC}Repositories` in the context's folder of the repository project ([`Catalog/DependencyInjection.cs`][catalog-repository-di]) | each repository once, scoped, with each port forwarding to it |
| 2 · provider-agnostic | `AddECommerceRepositories(connectionStringName, sqlDialect, migrationRunner)` ([`Repository/DependencyInjection.cs`][repository-di]) | every `Add{BC}Repositories`, the `IUnitOfWork` adapter, the migrator adapter |
| 3 · provider | `AddECommercePostgres` / `AddECommerceMySql` ([Postgres][postgres-di], [MySQL][mysql-di]) | the keyed unit of work and db repository, the dialect singletons, then level 2 with the matched dialect and runner |
| 4 · host | [`Persistence/DependencyInjection.cs`][host-persistence-di] | one level-3 entry, chosen by the `Database:Provider` switch |

Level 1 registers the concrete class once and forwards both ports to it, so the Reader and the Write port share
one instance per scope:

```csharp
services.AddScoped<ProductsRepository>();
services.AddScoped<IProductsReaderPort>(provider => provider.GetRequiredService<ProductsRepository>());
services.AddScoped<IProductsWritePort>(provider => provider.GetRequiredService<ProductsRepository>());
```

Level 3 fails fast and keys the framework unit of work by the application's `AppKey`
([`Postgres/DependencyInjection.cs`][postgres-di]):

```csharp
var connectionString = configuration.GetConnectionString(ConnectionStringName);
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException($"Missing ConnectionStrings:{ConnectionStringName}.");

services.AddPostgresUnitOfWork(ApplicationConfig.AppKey, connectionString);
services.AddPostgresDbRepository(ApplicationConfig.AppKey);
services.AddSingleton<IMemberSqlDialect, PostgresMemberSqlDialect>();
return services.AddECommerceRepositories(ConnectionStringName, new PostgresSqlDialect(), new PostgresMigrationRunner());
```

- The framework unit of work is keyed by `ApplicationConfig.AppKey`, which gives the business connection pool its
  own identity, separate from other consumers such as the saga store. `AddPostgresDbRepository(AppKey)` registers
  the scoped `IAxisDbRepository` bound to that keyed unit of work, so repositories simply inject
  `IAxisDbRepository`.
- The single `IUnitOfWork` implementation wraps the keyed framework unit of work
  ([`UnitOfWorkAdapter`][uow-adapter]):

  ```csharp
  internal sealed class UnitOfWorkAdapter(
      [FromKeyedServices(ApplicationConfig.AppKey)] IAxisUnitOfWork inner) : IUnitOfWork
  {
      public Task<AxisResult> SaveChangesAsync() => inner.SaveChangesAsync();

      public Task<AxisResult> RenewAsync() => inner.RenewAsync();
  }
  ```

- Per-entity dialect implementations are stateless and registered as singletons by the provider project.
- Provider selection happens only at the host, through configuration. No repository code branches on the provider.
- Each provider entry point fails fast when its own connection string is missing.

---

## Unit of work

There is exactly one unit-of-work contract for all repositories, at the root namespace of the driven contracts,
with exactly two members ([`IUnitOfWork`][iunit-of-work]):

```csharp
public interface IUnitOfWork
{
    Task<AxisResult> SaveChangesAsync();
    Task<AxisResult> RenewAsync();
}
```

Write ports enlist their work in the ambient connection and transaction; nothing commits until the component
that owns the business transaction calls `SaveChangesAsync`, **once**, at the tail of its railway.

| Component | Injects `IUnitOfWork`? | Reference application |
|---|---|---|
| command handler | yes; commits once, after every write-port call | [`MarkEmailValidatedHandler`][mark-email-validated-handler], [`CreateProductHandler`][create-product-handler] |
| saga stage handler that writes through its own port | yes | [`RemoveValidationCodeStageHandler`][remove-code-stage] |
| saga stage handler that writes only through another bounded context's facade | no; that facade's handler owns the commit | [`RevertEmailValidatedStageHandler`][revert-email-stage] |
| bus event handler | yes, as an entry point | [`ProductCheckedOutEventHandler`][product-checked-out-handler] |
| application service acting as an entry point (orchestration, reconciler) | yes | |
| query handler | never | [`GetValidationStatusHandler`][get-validation-status-handler] |
| validator, factory, port | never | [`ProductFactory`][product-factory] |

The checkout shows the whole discipline ([`CheckoutProductHandler`][checkout-handler]):

```csharp
return mediator.AxisEntityId.Rop()
    .Ensure(caller => caller.HasValue, SharedErrors.CustomerIdentityRequired)
    .ThenAsync(_ => unitOfWork.RenewAsync())
    .ThenAsync(_ => productsWrite.GetByIdForUpdateAsync(productId))
    .EnsureAsync(product => product.Stock >= command.Quantity, ProductErrors.InsufficientStock)
    .ThenAsync(product => productsWrite.ReserveStockAsync(product.ProductId, command.Quantity!.Value))
    .ThenAsync(product => bus.PublishAsync(
            new ProductCheckedOutEvent(
                command.CartId!,
                product.ProductId,
                command.Quantity!.Value
            ), ProductCheckedOutEvent.Topic))
    .ThenAsync(_ => unitOfWork.SaveChangesAsync())
    .MapAsync(product => new CheckoutProductResponse
    {
        CustomerId = mediator.AxisEntityId!.Value,
        ProductId = product.ProductId,
    });
```

- [ ] `SaveChangesAsync` appears once per logical transaction, inside the chain (the success arm of the terminal
      `Then` or `Match`), after every write. A use case that mutates several aggregates still commits once.
- [ ] A bus event is published with `IAxisBus.PublishAsync` **inside** the chain, **before** `SaveChangesAsync`.
      With the atomic outbox of [AxisBus.Postgres](https://github.com/axis-sys/axis-bus-postgres) or
      [AxisBus.MySql](https://github.com/axis-sys/axis-bus-mysql), the event is written in the same transaction
      as the change it announces and drained only once that transaction commits. The analyzers of
      [AxisBus](https://github.com/axis-sys/axis-bus) flag a `PublishAsync` placed after the last
      `SaveChangesAsync` of a member (AXIS0300).
- [ ] `RenewAsync` is called only before a locked read-then-act, never as a substitute for `SaveChangesAsync`.

**Why `RenewAsync`.** Engines differ on read isolation: under a repeatable-read snapshot (MySQL) a later read can
see data a read-committed engine (Postgres) would have refreshed. `RenewAsync` restarts a transaction that has not
written yet, so the next read takes a fresh snapshot; once a write has run it refuses with a `Conflict` instead of
discarding work. Its rollback also releases the row locks a locking read took, so renew **before** the locking
read, never between it and the act.

**Exceptions.**

- An orchestration service that deliberately commits per iteration, to release a per-row transactional lock, may
  call `SaveChangesAsync` more than once when each commit is a required lock boundary.
- An idempotent no-op branch that stages nothing skips `SaveChangesAsync` entirely, as the already-validated branch
  of `MarkEmailValidatedHandler` does.

The unit-test host proves the order renew, locking read, write ([`CheckoutProductHandlerTests`][checkout-handler-tests]);
the saga compensation ends `Compensated` and leaves the email unvalidated on both providers ([`ValidateEmailSagaCompensationDualDbTests`][saga-compensation-tests]).

---

## Errors, not exceptions

Repository methods contain **zero** `try/catch`. Translating a driver exception into a typed `AxisError` is owned
by `IAxisDbRepository` and the migration runner; the repository only names the code to surface for the two
expected outcomes:

| Outcome | Where the code goes | Example |
|---|---|---|
| duplicate key on a write | `duplicateKeyCode:` of `ExecuteAsync` / `ExecuteCountAsync` | `duplicateKeyCode: ProductErrors.SkuAlreadyExists.Code` |
| no row on a single-row read | the `notFoundCode` of `GetAsync` | `ProductErrors.ProductNotFound.Code` |

Both are the `Code` of a `static readonly AxisError` member of the aggregate's `{Aggregate}Errors` class, beside
its ports ([`ProductErrors`][product-errors]):

```csharp
public static class ProductErrors
{
    public const string ProductIdInvalid = "PRODUCT_ID_INVALID";
    public static readonly AxisError ProductNotFound = AxisError.NotFound("PRODUCT_NOT_FOUND");

    public static readonly AxisError InsufficientStock = AxisError.BusinessRule("INSUFFICIENT_STOCK");

    public const string SkuInvalid = "SKU_INVALID";
    public const string InitialStockInvalid = "INITIAL_STOCK_INVALID";
    public static readonly AxisError SkuAlreadyExists = AxisError.Conflict("PRODUCT_SKU_ALREADY_EXISTS");
}
```

- Never a string literal and never a const declared in the repository.
- That class is the **single declaration**: the application returns and matches the same member
  (`RequireNotFoundAsync(ProductErrors.SkuAlreadyExists)` in `CreateProductHandler`), and so do the tests. No other
  layer re-declares it.
- An error code is a contract: it is never renamed once delivered.
- A driven adapter's own failure is also a member of the errors class beside the port it implements, never an
  adapter-local class or a private const: [`HttpExternalIdentityContext`][external-identity-context] returns
  `AuthErrors.ExternalTokenInvalid`.
- Nothing throws for flow control; every method returns `Task<AxisResult>` or `Task<AxisResult<T>>`.

**Exception.** The migrator adapter wraps its run, because the framework runner executes raw scripts outside
`IAxisDbRepository`. The reference application does it with `AxisResult.TryAsync` mapping the exception to
`AdminErrors.DatabaseMigrationFailed`
([AxisResult · Exceptions at the boundary](https://github.com/axis-sys/axis-result/blob/main/docs/boundary-and-try.md)):

```csharp
public Task<AxisResult<IReadOnlyList<string>>> MigrateAsync()
    => configuration.GetConnectionString(connectionStringName).Rop()
        .Ensure(connectionString => !string.IsNullOrWhiteSpace(connectionString), AdminErrors.DatabaseConnectionStringMissing)
        .ThenAsync(connectionString => AxisResult.TryAsync(() => RunAsync(connectionString!), Failed));
```

---

## Connection pools

The business repositories (keyed by `AppKey`) and the saga store draw from **different pools**, even when they
target the same database, so background fan-out cannot starve request handling.

| Invariant | Reference application |
|---|---|
| the business pool is sized in its own connection string | `Maximum Pool Size=20` in [`appsettings.Development.json`][appsettings-development] |
| the saga pool is sized by its own key, applied by rewriting the saga connection string at the host, never by mutating the business string | `Saga:MaxPoolSize`, applied with [`ConnectionPools.WithMaxPoolSize`][connection-pools] |
| the pool arithmetic is guarded: the pools sharing one server must fit its connection limit, or the host refuses to start | `Database:MaxConnections` ([`appsettings.json`][appsettings]) |
| concurrency knobs that protect the pools (an orchestration window, a global maximum of concurrent sagas) are configuration or seeded settings, not constants | no such knob yet; the worker toggles (`Saga:ResumerEnabled`, `Bus:DispatcherEnabled`, …) are configuration the same way ([`PersistenceKeys`][host-keys]) |

[`Persistence/DependencyInjection.cs`][host-persistence-di]:

```csharp
var sagaProvider = PersistenceConfiguration.Provider(configuration, PersistenceKeys.SagaProvider);
var sagaPoolSize = PersistenceConfiguration.PositiveInt(configuration, PersistenceKeys.SagaMaxPoolSize);
var sagaConnectionString = ConnectionPools.WithMaxPoolSize(
    sagaProvider, PersistenceConfiguration.ConnectionString(configuration, sagaProvider), sagaPoolSize);

EnsurePoolsFitTheServer(configuration, businessProvider, businessConnectionString, sagaProvider, sagaPoolSize);
```

The budget counts every pool sized from the business string (the unit of work, the outbox dispatcher and the
cache) plus the saga pool when the saga shares the engine. The composition is proven in
[`PersistenceCompositionTests`][composition-tests]: a missing saga pool size, or pools that overflow
the budget, refuse to compose.

**Exception.** Test fixtures pin their own smaller pools to fit container limits ([`DualDbFixture`][dual-db-fixture]).

---

## Multiple providers

Targeting several relational engines is an application choice; the framework enables it without mandating it.
The reference application targets Postgres and MySQL. When you do, parity is **total**: the provider is a runtime
switch, never a compile-time fork, and every persistence change is written once and tested on each engine.

- [ ] Repository logic lives once, in the provider-agnostic project; each per-engine project holds only provider
      wiring and dialect seams ([`MySql/DependencyInjection.cs`][mysql-di]).
- [ ] The host references every targeted provider ([`Scaffolds.ECommerce.Host.csproj`][host-csproj]) and picks one
      by configuration only: no conditional compilation, no provider-branching path in the core.
- [ ] No repository references a provider driver type; repositories are written against `IAxisDbRepository` and
      `DbDataReader`, with provider-specific SQL behind dialect seams.
- [ ] Central package versions carry every targeted driver and every test-container module
      ([`Scaffolds.ECommerce.IntegrationTests.csproj`][integration-csproj]: `Npgsql`, `MySqlConnector`,
      `Testcontainers.PostgreSql`, `Testcontainers.MySql`).
- [ ] Integration tests reference every provider, and any invariant involving locking, transactions or ordering is
      asserted against each engine through dual fixtures ([`ProductsRepositoryDualDbTests`][products-dualdb-tests]).
- [ ] A session that stores UTC timestamps runs as a UTC session, and read-then-act-under-lock code is verified per
      engine, because snapshot semantics differ.

**Exceptions.** An application that targets a single provider ships and tests against that one engine. End-to-end
tests may exercise a single, pinned provider while the multi-engine matrix is proven at the integration layer
([Testing](testing.md)).

---

## Concurrency traps

The same SQL runs on engines whose isolation and locking differ (read-committed versus repeatable-read with gap
locks). A pattern that is safe on one engine can silently break on the other, so concurrent invariants use
portable patterns:

| Situation | Portable pattern | Never |
|---|---|---|
| read-then-act under a row lock | `RenewAsync` first, then the locking read, then the act | act on a snapshot pinned by an earlier read |
| row-level serialization | a portable `SELECT ... FOR UPDATE` by key in shared SQL (`ProductsRepository.GetByIdForUpdateAsync`) | advisory locks or provider-specific lock syntax in shared code |
| idempotent insert | each provider's do-nothing-on-conflict form behind a dialect member | a provider-specific upsert inlined in shared code |
| create-or-replace of one key | a create that declares its duplicate-key code, recovered by an update (`ValidationCodeFactory`, `MemberFactory`) | a provider upsert |
| a cap ("at most N") | gate with a plain read, then claim by primary key | a `COUNT` inside a locking statement on a shared path: range locks deadlock under repeatable read |
| timestamps | the UTC timestamp type, `DateTimeOffset` values written on a UTC session | local times |
| row identity in `ORDER BY` | a deterministic tiebreaker column | ordering with ties: engines break them differently |

A first-sight "ensure" renews before re-reading the winner of a lost race, so the snapshot pinned by its first read
does not hide the winner ([`EnsureCustomerHandler`][ensure-customer-handler]):

```csharp
private Task<AxisResult<ICustomerEntityProperties>> CreateAsync(EnsureCustomerCommand command)
    => customerFactory
        .CreateAsync(new ICustomerFactory.NewArgs
        {
            Email = command.Email!,
            Name = command.Name!,
            IsAdmin = options.Value.BootstrapAdminExternalIds.Contains(command.ExternalId!),
            ExternalId = command.ExternalId!,
            Provider = command.Provider,
        })
        .ThenAsync(_ => unitOfWork.SaveChangesAsync())
        .RecoverConflictAsync(() => ReadTheWinnerAsync(command.Email!));

private Task<AxisResult<ICustomerEntityProperties>> ReadTheWinnerAsync(string email)
    => unitOfWork
        .RenewAsync()
        .ThenAsync(() => customersReader.GetByEmailAsync(email));
```

The re-read can run on the same unit of work because the create declared its duplicate-key code: the duplicate
rolled back to a savepoint instead of faulting the transaction
([AxisResult · Recover](https://github.com/axis-sys/axis-result/blob/main/docs/recover.md)).

**Prove it on every engine.** A concurrency-sensitive invariant is not done until it passes against every targeted
provider; a green run on one engine proves nothing about another.

| Invariant | Proof in the reference application |
|---|---|
| 12 concurrent checkouts of a product with 5 in stock sell exactly 5; the rest fail with `INSUFFICIENT_STOCK` | [`CheckoutConcurrencyDualDbTests`][checkout-concurrency-tests] |
| concurrent first sights of one email provision one customer | [`EnsureCustomerConcurrencyDualDbTests`][ensure-customer-concurrency-tests] |
| many concurrent create-or-replace writers of one key: every outcome is a success or a `ServiceUnavailable` the client may retry, never an `InternalServerError`, and the row holds one of the written values | [`CreateOrReplaceConcurrencyDualDbTests`][create-or-replace-tests] |

**Why create-or-replace can fail transiently.** On MySQL the duplicate check of a losing create keeps a shared lock
on the existing row, which the recovering update must upgrade to an exclusive one, so two recoveries of the same
key deadlock. The engine rolls one back, and the provider adapter classifies the deadlock as transient: retried in
place while the transaction holds no write, surfaced as `ServiceUnavailable` otherwise. Postgres releases the locks
taken after the savepoint, so the same race only waits there. That retry lives inside the framework's provider
adapter, never in application or repository code.

**Exception.** An application that targets a single provider is exempt from the dual proof and the dialect seams;
the portable patterns still pay off if it may add a provider later.

---

## Tenant-scoped queries

This applies to multi-tenant applications only; the reference application is single-tenant. Tenant isolation is
enforced in SQL, per statement:

- [ ] A tenant-scoped table carries a tenant-key column, and a tenant-scoped port method takes the tenant key as an
      explicit (first) parameter, so the repository cannot be called without it.
- [ ] The repository binds the tenant key in the `WHERE` of **every** `SELECT`, `UPDATE` and `DELETE` it issues,
      joins included.
- [ ] A lookup by primary key still adds the tenant predicate: the primary key alone is not a tenant guard.
- [ ] Subqueries and `EXISTS` clauses re-apply the tenant filter on the inner table instead of relying on the outer
      filter.
- [ ] Any table under a tenant-scoped schema that carries the tenant-key column is tenant-scoped.

The tenant key comes from an ambient scoped accessor in the application and is already a validated value object
when it reaches the port. Deliberately cross-tenant tables (global identity, seeded lookup and status tables) and a
reconciler scan that must span tenants are the explicit exceptions.

---

## Checklist: a new table

1. **Ports and errors** in `{App}.Contracts.Driven/{BC}/{Aggregate}/`: `I{Entities}ReaderPort`, the
   `I{Entities}WritePort` if a use case mutates it, and the `{Aggregate}Errors` members for not-found and duplicate
   key.
2. **`{Entities}Columns`** (one const per column, `All` on one line) and **`{Entities}Table`** (`Table` const plus
   `Define()`), named in UPPER_SNAKE_CASE under the bounded context's schema.
3. **`{Entity}DbEntity`** with an ordinal `FromReader` in the order of `All`.
4. **The repository**: `internal sealed`, over `IAxisDbRepository`, a `Select` const, bound parameters, the
   `{Aggregate}Errors` codes; an `I{Entity}SqlDialect` only if some SQL differs between providers, implemented in
   each provider project.
5. **The DDL**: add `{Entities}Table.Define()` to the bounded context's `{BC}DbInit` V1 (before release) or to a
   new version (after).
6. **Registration**: the repository in `Add{BC}Repositories`, the dialect implementations as singletons in each
   provider project.
7. **Tests**: a dual-database repository test, plus a per-engine concurrency test for any locking or ordering
   invariant ([Testing](testing.md)).

## See also

- [Domain](domain.md): the aggregates, property interfaces and value objects these ports carry.
- [Architecture](architecture.md): the hexagonal projects and the swappable infrastructure ports.
- [Testing](testing.md): the dual-database fixture and the concurrency tests.
- [Style](style.md): naming and the `{Aggregate}Errors` convention.
- [AxisRepository](https://github.com/axis-sys/axis-repository) ·
  [AxisRepository.Postgres](https://github.com/axis-sys/axis-repository-postgres) ·
  [AxisRepository.MySql](https://github.com/axis-sys/axis-repository-mysql) ·
  [AxisResult](https://github.com/axis-sys/axis-result)

[iproducts-reader]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driven/Catalog/Products/IProductsReaderPort.cs
[iproducts-write]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driven/Catalog/Products/IProductsWritePort.cs
[product-errors]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driven/Catalog/Products/ProductErrors.cs
[iunit-of-work]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driven/IUnitOfWork.cs
[imigrator-port]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driven/Admin/IDatabaseMigratorPort.cs
[admin-errors]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driven/Admin/AdminErrors.cs
[products-repository]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Repository/Catalog/Products/ProductsRepository.cs
[products-columns]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Repository/Catalog/Products/ProductsColumns.cs
[products-table]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Repository/Catalog/Products/ProductsTable.cs
[customer-dbentity]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Repository/Customers/Customers/CustomerDbEntity.cs
[customers-columns]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Repository/Customers/Customers/CustomersColumns.cs
[member-dbentity]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Repository/Authorization/Members/MemberDbEntity.cs
[members-repository]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Repository/Authorization/Members/MembersRepository.cs
[imember-dialect]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Repository/Authorization/Members/IMemberSqlDialect.cs
[postgres-member-dialect]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Postgres/Authorization/Members/PostgresMemberSqlDialect.cs
[mysql-member-dialect]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.MySql/Authorization/Members/MySqlMemberSqlDialect.cs
[catalog-dbinit]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Repository/Catalog/CatalogDbInit.cs
[migrator-adapter]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Repository/DatabaseMigratorAdapter.cs
[uow-adapter]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Repository/UnitOfWorkAdapter.cs
[repository-di]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Repository/DependencyInjection.cs
[catalog-repository-di]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Repository/Catalog/DependencyInjection.cs
[postgres-di]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Postgres/DependencyInjection.cs
[mysql-di]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.MySql/DependencyInjection.cs
[external-identity-context]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.AuthProviders/HttpExternalIdentityContext.cs
[product-factory]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/Products/Aggregates/ProductFactory.cs
[checkout-handler]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/Products/UseCases/CheckoutProduct/v1/CheckoutProductHandler.cs
[create-product-handler]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/Products/UseCases/CreateProduct/v1/CreateProductHandler.cs
[mark-email-validated-handler]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Customers/UseCases/MarkEmailValidated/v1/MarkEmailValidatedHandler.cs
[ensure-customer-handler]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Customers/UseCases/EnsureCustomer/v1/EnsureCustomerHandler.cs
[remove-code-stage]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/EmailValidation/Sagas/ValidateEmail/Handlers/RemoveValidationCodeStageHandler.cs
[revert-email-stage]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/EmailValidation/Sagas/ValidateEmail/Handlers/RevertEmailValidatedStageHandler.cs
[get-validation-status-handler]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/EmailValidation/UseCases/GetValidationStatus/v1/GetValidationStatusHandler.cs
[product-checked-out-handler]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/CartItems/Events/ProductCheckedOutEventHandler.cs
[migrate-handler]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Admin/UseCases/MigrateDatabase/v1/MigrateDatabaseHandler.cs
[program]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Program.cs
[migrate-command]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Persistence/MigrateCommand.cs
[migrations-controller]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Controllers/Admin/DatabaseMigrationsV1Controller.cs
[host-persistence-di]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Persistence/DependencyInjection.cs
[connection-pools]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Persistence/ConnectionPools.cs
[host-keys]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Persistence/PersistenceKeys.cs
[appsettings]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/appsettings.json
[appsettings-development]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/appsettings.Development.json
[host-csproj]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Scaffolds.ECommerce.Host.csproj
[checkout-handler-tests]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.UnitTests/CheckoutProductHandlerTests.cs
[dual-db-fixture]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.IntegrationTests/DualDbFixture.cs
[integration-csproj]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.IntegrationTests/Scaffolds.ECommerce.IntegrationTests.csproj
[products-dualdb-tests]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.IntegrationTests/ProductsRepositoryDualDbTests.cs
[checkout-concurrency-tests]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.IntegrationTests/CheckoutConcurrencyDualDbTests.cs
[ensure-customer-concurrency-tests]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.IntegrationTests/EnsureCustomerConcurrencyDualDbTests.cs
[create-or-replace-tests]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.IntegrationTests/CreateOrReplaceConcurrencyDualDbTests.cs
[saga-compensation-tests]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.IntegrationTests/ValidateEmailSagaCompensationDualDbTests.cs
[composition-tests]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/PersistenceCompositionTests.cs
