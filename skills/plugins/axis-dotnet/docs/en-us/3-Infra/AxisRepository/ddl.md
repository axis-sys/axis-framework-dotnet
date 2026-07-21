# Schema DDL · `AxisTable`

> Declare a table **once** — columns, primary key, indexes, foreign keys, table-level checks and an optional idempotent seed — with the dialect-agnostic `Axis.Ddl` model, then hand it to an `IAxisSqlDialect` to render the concrete `CREATE TABLE` for Postgres or MySQL. Replaces hand-writing (and hand-syncing) one DDL string per database.

```csharp
namespace Axis.Persistence.Scripts;

internal static class WidgetsTable
{
    public const string Table    = $"{WidgetsSchema.Schema}.WIDGETS";
    public const string WidgetId = "WIDGET_ID";
    public const string OwnerId  = "OWNER_ID";
    public const string Title    = "TITLE";
    public const string IsActive = "IS_ACTIVE";
    public const string CreatedAt = "CREATED_AT";

    public static AxisTable Define() => new AxisTable(Table)
        .Column(WidgetId, AxisDbType.Varchar(50), primaryKey: true)
        .Column(OwnerId, AxisDbType.Varchar(50), notNull: true)
        .Column(Title, AxisDbType.Varchar(120), notNull: true, collation: AxisCollation.CaseAccentSensitive)
        .Column(IsActive, AxisDbType.Bool, notNull: true, @default: AxisDefault.Bool(true))
        .Column(CreatedAt, AxisDbType.TimestampUtc, notNull: true, @default: AxisDefault.NowUtc)
        .Index("IDX_WIDGETS_OWNER", OwnerId)
        .Unique("UX_WIDGETS_OWNER_TITLE", OwnerId, Title)
        .ForeignKey("FK_WIDGETS_OWNER", OwnerId, OwnersTable.Table, OwnersTable.OwnerId, onDeleteCascade: true);
}
```

`Define().Render(dialect)` — called once per adapter with `new PostgresSqlDialect()` or `new MySqlSqlDialect()` — is what a `{BC}DbInit`/`{Package}Schema` class hands to [`IAxisMigrationRunner`](migrations.md) as a migration script. This page covers the model; [Migrations](migrations.md) covers applying it.

---

## When to use

Any table whose DDL must run identically — structurally — on every database adapter your package or Bounded Context ships. One `AxisTable` definition, rendered per dialect, is the single source of truth for column names, types and constraints; the two adapters can never drift apart because there is only one place to edit.

## When *not* to use

| You want to… | Use instead |
|---|---|
| **alter** a table already created by a prior `Define()` | a new versioned migration script — [`RenderAddColumn`](#adding-a-column--renderaddcolumn) for a portable `ADD COLUMN`; raw SQL for any other `ALTER` (`AxisTable` itself only renders `CREATE TABLE IF NOT EXISTS`) |
| apply the rendered DDL to a real database | [`IAxisMigrationRunner`](migrations.md) — this page only builds the string |
| target a database with no `IAxisSqlDialect` yet | implement one over [`AxisSqlDialectBase`](#the-dialect--iaxissqldialect-and-axissqldialectbase) |

---

## The builder — `AxisTable` fluent methods

Every method returns `this`, so a definition reads as one chained expression. `Name` (the qualified `{SCHEMA}.{TABLE}`) is fixed at construction.

| Method | Adds | Notes |
|---|---|---|
| `Column(name, dbType, notNull, default, primaryKey, check, collation)` | one column | `dbType` via [`AxisDbType.*`](#column-types--axisdbtype), `default` via [`AxisDefault.*`](#column-defaults--axisdefault) |
| `Index(name, params columns)` | a plain, non-unique index | rendered as a standalone `CREATE INDEX` (Postgres) or inlined in the table body (MySQL) |
| `Unique(name, params columns)` | a unique index | same rendering split as `Index` |
| `PartialIndex(name, predicate, params columns)` | a conditional non-unique index | Postgres: `CREATE INDEX ... WHERE {predicate}`; MySQL has no partial indexes, so the predicate is dropped and it becomes a plain index |
| `PartialUnique(name, predicate, params columns)` | a conditional unique index | Postgres: `WHERE {predicate}`; MySQL emulates it with a `GENERATED ALWAYS ... STORED` column that is `NULL` outside the predicate (unique keys ignore `NULL`, so rows outside the predicate never collide) |
| `ForeignKey(name, column, referencedTable, referencedColumn, onDeleteCascade)` | a table-level FK constraint | every dialect renders it identically: `CONSTRAINT {name} FOREIGN KEY (...) REFERENCES ...` |
| `Check(name, expression)` | a table-level `CHECK` constraint | `expression` is raw, portable SQL (e.g. a cross-column XOR) rendered verbatim by every dialect — see also the column-level [`AxisCheck.IsTrue`](#column-level-checks-and-collation) |
| `WithSeed(columns, conflictColumns, rows)` | one idempotent `INSERT` | no-op on a conflict of `conflictColumns` — see [Seeding](#seeding--axisseed) |
| `Render(dialect)` | — | returns the full DDL string (`CREATE TABLE` + indexes + seed) for the given `IAxisSqlDialect` |

`Columns`, `Indexes`, `ForeignKeys`, `Checks` and `Seed` are exposed as read-only properties — a dialect (or a test) inspects the recorded model without re-parsing SQL.

---

## Column types · `AxisDbType`

A closed set of logical types; the dialect maps each to the concrete column type.

| Factory | Postgres | MySQL |
|---|---|---|
| `Varchar(length)` | `VARCHAR(length)` | `VARCHAR(length)` |
| `Text` | `TEXT` | `TEXT` |
| `Int` | `INT` | `INT` |
| `Bool` | `BOOLEAN` | `TINYINT(1)` |
| `Json` | `JSONB` | `JSON` |
| `TimestampUtc` | `TIMESTAMPTZ` | `DATETIME(6)` |
| `Decimal(precision, scale)` | `NUMERIC(precision,scale)` | `DECIMAL(precision,scale)` |

The scalar factories (`Text`, `Int`, `Bool`, `Json`, `TimestampUtc`) are singletons; `Varchar`/`Decimal` carry their arguments.

## Column defaults · `AxisDefault`

| Factory | Renders to |
|---|---|
| `NowUtc` | Postgres `NOW()` · MySQL `(UTC_TIMESTAMP(6))` |
| `Bool(value)` | Postgres `TRUE`/`FALSE` · MySQL `1`/`0` |
| `Int(value)` | the literal, always culture-invariant |
| `Raw(sql)` | `sql` verbatim — the escape hatch for a vendor-specific default the model has no factory for (e.g. `"gen_random_uuid()"`) |

## Column-level checks and collation

`AxisCheck.IsTrue` pins a boolean column to `true` — the single-row-guard pattern for a settings table with exactly one row (`ONLY_ROW BOOLEAN PRIMARY KEY CHECK (...)`, as `AXIS_SAGA.SAGA_SETTINGS` uses it). Postgres renders `CHECK (col)`; MySQL renders `CHECK (col = 1)`.

`AxisCollation` pins per-column string comparison intent, because the two databases disagree on defaults: Postgres compares case- and accent-sensitively via `=`; MySQL's default collation (`utf8mb4_0900_ai_ci`) folds both case and accent. Set the intent explicitly so MySQL matches Postgres semantics:

| Value | Use for |
|---|---|
| `Default` | no explicit collation — accept the dialect's own default |
| `CaseAccentSensitive` | exact equality / unique keys (MySQL: `COLLATE utf8mb4_0900_as_cs`) |
| `CaseInsensitiveAccentSensitive` | `ILIKE`/`lower()`-style search (MySQL: `COLLATE utf8mb4_0900_as_ci`) |

Postgres ignores `AxisCollation` (its default already matches `CaseAccentSensitive`); only the MySQL dialect renders a `COLLATE` clause.

## Seeding · `AxisSeed`

`WithSeed(columns, conflictColumns, rows)` records one idempotent `INSERT`, applied the first time the table's migration script runs:

```csharp
.WithSeed(
    columns:         [OnlyRow, MaxConcurrentSagas],
    conflictColumns: [OnlyRow],
    new object?[] { true, 20 });
```

Renders as an `ON CONFLICT (...) DO NOTHING` on Postgres and `ON DUPLICATE KEY UPDATE col = col` on MySQL — never `INSERT IGNORE`, which would also silently swallow FK and `NOT NULL` violations instead of only skipping the intended duplicate. Row values go through the same `RenderValue` used everywhere: numeric literals are always culture-invariant (a culture-specific `ToString()` would emit a decimal comma and corrupt the value), and `DateTime`/`DateTimeOffset` values normalize to UTC before rendering.

---

## The dialect — `IAxisSqlDialect` and `AxisSqlDialectBase`

```csharp
public interface IAxisSqlDialect
{
    string RenderCreateTable(AxisTable table);
    string RenderAddColumn(string table, AxisColumn column);
}
```

Two methods: `RenderCreateTable` turns an `AxisTable` into the full DDL for one database; `RenderAddColumn` renders one portable `ALTER TABLE … ADD COLUMN` for an incremental migration — see [Adding a column](#adding-a-column--renderaddcolumn). `AxisSqlDialectBase` implements `RenderCreateTable` once — column assembly order (type + collation → `PRIMARY KEY`/`NOT NULL` → `DEFAULT` → `CHECK`), the `CREATE TABLE IF NOT EXISTS` wrapper, then post-table statements and the seed — and asks each concrete dialect for nine tokens:

| Hook | Answers |
|---|---|
| `RenderType` | the concrete column type for an `AxisDbType` |
| `RenderDefault` | the concrete `DEFAULT` expression for an `AxisDefault` |
| `RenderCheck` | the concrete column-level `CHECK` body |
| `RenderCollation` | the `COLLATE` clause (or `""`) for an `AxisCollation` |
| `RenderBoolLiteral` | how a bare `true`/`false` literal is spelled |
| `RenderSeedConflict` | the idempotent-insert clause (`ON CONFLICT` / `ON DUPLICATE KEY UPDATE`) |
| `RenderInlineIndexLines` | index lines to inline **inside** the `CREATE TABLE` body (MySQL) — `[]` if the dialect emits them separately |
| `RenderPostTableStatements` | statements to emit **after** the `CREATE TABLE` (Postgres's standalone `CREATE INDEX`s) — `[]` if the dialect inlines them |
| `RenderForeignKey` / `RenderTimestampLiteral` | abstract even though Postgres and MySQL currently agree — kept abstract so a future dialect with different FK syntax or timestamp handling isn't silently stuck with a default that doesn't fit it |

`PostgresSqlDialect` and `MySqlSqlDialect` (in `AxisRepository.Postgres` / `AxisRepository.MySql`) are the two shipped implementations — read them side by side to see exactly where Postgres and MySQL diverge: **indexing layout** is the big one — Postgres returns `[]` from `RenderInlineIndexLines` and emits standalone `CREATE INDEX IF NOT EXISTS ... ON table (...)` statements from `RenderPostTableStatements`; MySQL does the opposite, inlining `INDEX`/`UNIQUE KEY` lines (and, for a partial unique index, a `GENERATED ALWAYS ... STORED` column) into the `CREATE TABLE` body itself and returning `[]` from `RenderPostTableStatements`.

Shared, non-abstract helpers on the base class: `ForeignKeyConstraint` (the standard `CONSTRAINT ... FOREIGN KEY ... REFERENCES ...` both dialects reuse as-is), `Quote` (single-quote escaping), `FormatUtcTimestamp` (microsecond-precision UTC text) and `RenderNull` (defaults to `"NULL"`, overridable).

### Adding a column — `RenderAddColumn`

```csharp
dialect.RenderAddColumn(WidgetsTable.Table, new AxisColumn("UPLOADED_AT", AxisDbType.TimestampUtc));
// Postgres: ALTER TABLE {schema}.WIDGETS ADD COLUMN UPLOADED_AT TIMESTAMPTZ;
// MySQL:    ALTER TABLE {schema}.WIDGETS ADD COLUMN UPLOADED_AT DATETIME(6);
```

One `ALTER TABLE {table} ADD COLUMN …;` statement per call, for the new-version script that evolves an already-shipped table — see [Migrations](migrations.md). The column line goes through the **same pipeline** `RenderCreateTable` uses, so the dialect owns the type mapping and you never hand-write engine tokens. `virtual` on `AxisSqlDialectBase`, so a future dialect with divergent `ALTER` syntax can override it. Three caveats:

- **No `IF NOT EXISTS`** — deliberate: MySQL has no such form for `ADD COLUMN`. Idempotency comes from the migration ledger, which never re-applies a recorded version — put the statement in a new version and it runs exactly once.
- **A `PRIMARY KEY` column throws `ArgumentException`** — adding a primary key via `ALTER TABLE` is not portable; declare it in the `CREATE TABLE`.
- **`NOT NULL` without a `DEFAULT`** fails on Postgres when the table has rows (MySQL backfills an implicit default) — give the column a default, or add it nullable.

---

## Real-world examples

### 1. A multi-index table shared by two adapters — `AxisSaga`

```csharp
public static AxisTable Define() => new AxisTable(Table)
    .Column(SagaId, AxisDbType.Varchar(50), primaryKey: true)
    .Column(SagaName, AxisDbType.Varchar(100), notNull: true)
    .Column(Status, AxisDbType.Varchar(30), notNull: true)
    .Column(PayloadJson, AxisDbType.Json, notNull: true)
    .Column(Version, AxisDbType.Int, notNull: true, @default: AxisDefault.Int(1))
    .Column(ClaimedUntil, AxisDbType.TimestampUtc)
    // …
    .Index("IDX_SAGA_INSTANCES_STATUS_UPDATED", Status, UpdatedAt)
    .Index("IDX_SAGA_INSTANCES_LEASE", Status, ClaimedUntil)
    .PartialIndex("IDX_SAGA_INSTANCES_ACTIVE_LEASE",
        $"{Status} NOT IN ('Completed','Failed','Compensated')", ClaimedUntil);
```

`AxisSagaSchema.Tables` lists this alongside three sibling tables; `AxisSagaMigrations.InitializePostgresAsync` renders them with `new PostgresSqlDialect()`, `AxisSagaMySqlMigrations.InitializeMySqlAsync` renders the exact same `AxisTable` instances with `new MySqlSqlDialect()`. See [Database schema · `AXIS_SAGA`](../../2-ApplicationFlow/AxisSaga/database-schema.md) for the full column-by-column reference.

**Why it pays off:** the partial index that keeps the lease-count query selective is declared once. A column rename or a new index is one edit that reaches both adapters — there is no second DDL string to remember to update.

### 2. A single-table schema — `AxisCache`

```csharp
internal static class CacheEntriesTable
{
    public const string Schema = "AXIS_CACHE";
    public const string Table  = $"{Schema}.CACHE_ENTRIES";

    public static AxisTable Define() => new AxisTable(Table)
        .Column(CacheKey, AxisDbType.Varchar(200), primaryKey: true)
        .Column(ValueJson, AxisDbType.Json, notNull: true)
        .Column(ExpiresAt, AxisDbType.TimestampUtc)
        .Column(UpdatedAt, AxisDbType.TimestampUtc, notNull: true, @default: AxisDefault.NowUtc)
        .Index("IDX_CACHE_ENTRIES_EXPIRES_AT", ExpiresAt);
}

public static class AxisCacheSchema
{
    public const string Schema = CacheEntriesTable.Schema;

    public static (string Version, string Script)[] Migrations(IAxisSqlDialect dialect) =>
        [("V1", CacheEntriesTable.Define().Render(dialect))];
}
```

**Why it pays off:** `{Package}Schema.Migrations(dialect)` is the one line every adapter's initializer calls — see [Migrations](migrations.md) for what runs it.

---

## See also

- [Migrations · `IAxisMigrationRunner`](migrations.md) — applies the DDL this page builds, idempotently
- [Postgres adapter](postgres-adapter.md) — ships `PostgresSqlDialect` and `PostgresMigrationRunner`
- [MySQL adapter](mysql-adapter.md) — ships `MySqlSqlDialect` and `MySqlMigrationRunner`
- [Repository base](repository-base.md) — the repository that reads/writes the rows this schema defines
- [Database schema · `AXIS_SAGA`](../../2-ApplicationFlow/AxisSaga/database-schema.md) — a full worked example of an `AxisTable`-defined schema

---

↩ [Back to AxisRepository docs](README.md)
