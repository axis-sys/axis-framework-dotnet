namespace Axis.Ddl;

/// <summary>
/// A dialect-agnostic table definition. Declare a table ONCE — columns, primary key, indexes, foreign keys
/// and an optional idempotent seed — and an <see cref="IAxisSqlDialect"/> renders the dialect-specific DDL.
/// Replaces hand-writing one <c>CREATE TABLE</c> per database. Fluent methods return <c>this</c>.
/// </summary>
public sealed class AxisTable(string name)
{
    private readonly List<AxisColumn> _columns = [];
    private readonly List<AxisIndex> _indexes = [];
    private readonly List<AxisForeignKey> _foreignKeys = [];
    private readonly List<AxisTableCheck> _checks = [];

    /// <summary>The qualified table name, e.g. <c>AXIS_SAGA.SAGA_INSTANCES</c>.</summary>
    public string Name { get; } = name;

    public IReadOnlyList<AxisColumn> Columns => _columns;
    public IReadOnlyList<AxisIndex> Indexes => _indexes;
    public IReadOnlyList<AxisForeignKey> ForeignKeys => _foreignKeys;
    public IReadOnlyList<AxisTableCheck> Checks => _checks;
    public AxisSeed? Seed { get; private set; }

    /// <summary>A composite primary key declared at table level via <see cref="PrimaryKey"/>. Null when the table
    /// uses (or has none of) the per-column <c>primaryKey: true</c> flag instead.</summary>
    public IReadOnlyList<string>? PrimaryKeyColumns { get; private set; }

    public AxisTable Column(
        string name,
        AxisDbType dbType,
        bool notNull = false,
        AxisDefault? @default = null,
        bool primaryKey = false,
        AxisCheck? check = null,
        AxisCollation collation = AxisCollation.Default)
    {
        _columns.Add(new AxisColumn(name, dbType, notNull, @default, primaryKey, check, collation));
        return this;
    }

    /// <summary>
    /// Declares a composite primary key at table level (e.g. a natural key <c>(TenantId, Slug)</c>), rendered as a
    /// single <c>PRIMARY KEY (col1, col2, …)</c> table constraint — never one per column. For a single-column key
    /// use the <c>primaryKey: true</c> flag on <see cref="Column"/> instead; combining both on the same table is
    /// rejected at render time.
    /// </summary>
    public AxisTable PrimaryKey(params string[] columns)
    {
        if (columns.Length < 2)
            throw new ArgumentException(
                "PrimaryKey requires at least 2 columns to form a composite key; use the per-column primaryKey: true flag on Column for a single-column primary key.",
                nameof(columns));

        PrimaryKeyColumns = columns;
        return this;
    }

    public AxisTable Index(string name, params string[] columns)
    {
        _indexes.Add(new AxisIndex(name, columns));
        return this;
    }

    /// <summary>A conditional (partial) non-unique index — For databases like Postgres <c>WHERE {predicate}</c>; MySQL, for example, drops the predicate.</summary>
    public AxisTable PartialIndex(string name, string predicate, params string[] columns)
    {
        _indexes.Add(new AxisIndex(name, columns, Unique: false, PartialPredicate: predicate));
        return this;
    }

    public AxisTable Unique(string name, params string[] columns)
    {
        _indexes.Add(new AxisIndex(name, columns, Unique: true));
        return this;
    }

    /// <summary>A conditional UNIQUE index — For databases like Postgres <c>WHERE {predicate}</c>; MySQL, for example, emulates it via a generated column.</summary>
    public AxisTable PartialUnique(string name, string predicate, params string[] columns)
    {
        _indexes.Add(new AxisIndex(name, columns, Unique: true, PartialPredicate: predicate));
        return this;
    }

    public AxisTable ForeignKey(string name, string column, string referencedTable, string referencedColumn, bool onDeleteCascade = false)
    {
        _foreignKeys.Add(new AxisForeignKey(name, column, referencedTable, referencedColumn, onDeleteCascade));
        return this;
    }

    /// <summary>A table-level <c>CHECK</c> constraint with a portable boolean expression (e.g. a cross-column XOR).</summary>
    public AxisTable Check(string name, string expression)
    {
        _checks.Add(new AxisTableCheck(name, expression));
        return this;
    }

    /// <summary>An idempotent seed (no-op on a conflict of <paramref name="conflictColumns"/>).</summary>
    public AxisTable WithSeed(IReadOnlyList<string> columns, IReadOnlyList<string> conflictColumns, params object?[][] rows)
    {
        Seed = new AxisSeed(columns, conflictColumns, rows);
        return this;
    }

    /// <summary>Renders the full DDL (CREATE TABLE + indexes + seed) for the given dialect.</summary>
    public string Render(IAxisSqlDialect dialect) => dialect.RenderCreateTable(this);
}
