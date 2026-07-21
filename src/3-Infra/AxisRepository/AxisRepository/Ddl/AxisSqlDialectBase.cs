using System.Globalization;
using System.Text;

namespace Axis.Ddl;

/// <inheritdoc/>
public abstract class AxisSqlDialectBase : IAxisSqlDialect
{
    // --- Dialect-specific tokens: every adapter MUST decide these. ---
    protected abstract string RenderType(AxisDbType dbType);
    protected abstract string RenderDefault(AxisDefault @default);
    protected abstract string RenderCheck(AxisCheck check, string column);
    protected abstract string RenderCollation(AxisCollation collation);
    protected abstract string RenderBoolLiteral(bool value);
    protected abstract string RenderSeedConflict(AxisSeed seed);
    protected abstract IEnumerable<string> RenderInlineIndexLines(AxisTable table);
    protected abstract IEnumerable<string> RenderPostTableStatements(AxisTable table);

    /// <summary>
    /// Renders one foreign key. Abstract on purpose: Postgres and MySQL render it identically today (both
    /// delegate to <see cref="ForeignKeyConstraint"/>), but forcing each adapter to opt in keeps a future
    /// dialect with different FK syntax from inheriting a default that doesn't fit it.
    /// </summary>
    protected abstract string RenderForeignKey(AxisForeignKey fk);

    /// <summary>
    /// Renders a UTC instant as a SQL literal. Abstract because the safe form differs per dialect — Postgres
    /// pins the offset (<c>TIMESTAMPTZ '… +00'</c>); MySQL stores the UTC wall-clock in a <c>DATETIME</c>.
    /// Callers normalize to UTC first (see <see cref="RenderValue"/>), so there is no timezone loss here.
    /// </summary>
    protected abstract string RenderTimestampLiteral(DateTimeOffset utc);

    /// <summary>The standard <c>CONSTRAINT … FOREIGN KEY … REFERENCES …</c> rendering, reusable by adapters.</summary>
    protected string ForeignKeyConstraint(AxisForeignKey fk)
        => $"CONSTRAINT {fk.Name} FOREIGN KEY ({fk.Column}) REFERENCES {fk.ReferencedTable} ({fk.ReferencedColumn})"
           + (fk.OnDeleteCascade ? " ON DELETE CASCADE" : "");

    /// <summary>A table-level CHECK constraint. The expression is portable SQL, so every dialect renders it the same.</summary>
    protected virtual string RenderTableCheck(AxisTableCheck check) => $"CONSTRAINT {check.Name} CHECK ({check.Expression})";

    /// <summary>The SQL null token. Defaults to <c>NULL</c>; override if a provider needs a different literal.</summary>
    protected virtual string RenderNull() => "NULL";

    /// <summary>Single-quotes a string literal, escaping embedded quotes. Shared by every adapter.</summary>
    protected static string Quote(string value) => "'" + value.Replace("'", "''") + "'";

    /// <summary>Canonical microsecond-precision UTC text (<c>yyyy-MM-dd HH:mm:ss.ffffff</c>) for timestamp literals.</summary>
    protected static string FormatUtcTimestamp(DateTimeOffset utc)
        => utc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture);

    public virtual string RenderCreateTable(AxisTable table)
    {
        var body = new List<string>();
        body.AddRange(table.Columns.Select(RenderColumn));

        if (table.PrimaryKeyColumns is { } pkColumns)
        {
            // A composite PRIMARY KEY is a single table-level constraint (same syntax on Postgres and MySQL), never
            // one PRIMARY KEY clause per column — that would emit N constraints and both engines reject the table.
            if (table.Columns.Any(c => c.PrimaryKey))
                throw new InvalidOperationException(
                    $"Table '{table.Name}' declares both a composite PrimaryKey({string.Join(", ", pkColumns)}) and a "
                    + "column-level primaryKey: true. Use one or the other: PrimaryKey(...) for a composite key, "
                    + "primaryKey: true on Column for a single-column key.");

            body.Add($"PRIMARY KEY ({string.Join(", ", pkColumns)})");
        }

        body.AddRange(RenderInlineIndexLines(table));
        body.AddRange(table.ForeignKeys.Select(RenderForeignKey));
        body.AddRange(table.Checks.Select(RenderTableCheck));

        var sb = new StringBuilder();
        sb.AppendLine($"CREATE TABLE IF NOT EXISTS {table.Name}");
        sb.AppendLine("(");
        sb.AppendLine(string.Join(",\n", body.Select(line => "    " + line)));
        sb.AppendLine(");");

        foreach (var statement in RenderPostTableStatements(table))
            sb.AppendLine(statement);

        if (table.Seed is { } seed)
            sb.Append(RenderSeed(table.Name, seed));

        return sb.ToString();
    }

    public virtual string RenderAddColumn(string table, AxisColumn column)
    {
        // DDL renders during bootstrap, before the app's railway starts, so a misuse throws (like the runner).
        if (column.PrimaryKey)
            throw new ArgumentException(
                $"Column '{column.Name}' is PRIMARY KEY: adding a primary key via ALTER TABLE is not portable — declare it in the CREATE TABLE.",
                nameof(column));

        return $"ALTER TABLE {table} ADD COLUMN {RenderColumn(column)};";
    }

    protected virtual string RenderColumn(AxisColumn column)
    {
        var parts = new List<string> { column.Name, RenderType(column.DbType) + RenderCollation(column.Collation) };
        if (column.PrimaryKey) parts.Add("PRIMARY KEY");
        else if (column.NotNull) parts.Add("NOT NULL");
        if (column.Default is { } d) parts.Add("DEFAULT " + RenderDefault(d));
        if (column.Check is { } c) parts.Add("CHECK (" + RenderCheck(c, column.Name) + ")");
        return string.Join(" ", parts);
    }

    protected virtual string RenderSeed(string table, AxisSeed seed)
    {
        var cols = string.Join(", ", seed.Columns);
        var rows = string.Join(",\n    ", seed.Rows.Select(r => "(" + string.Join(", ", r.Select(RenderValue)) + ")"));
        return $"INSERT INTO {table} ({cols}) VALUES\n    {rows}\n{RenderSeedConflict(seed)};\n";
    }

    protected virtual string RenderValue(object? value) => value switch
    {
        null => RenderNull(),
        bool b => RenderBoolLiteral(b),
        string s => Quote(s),

        // Numeric literals are NOT quoted, and ALWAYS invariant — a culture-specific ToString() would emit a
        // decimal comma (e.g. "1,5" on de-DE) and silently corrupt the value or break the INSERT.
        int i => i.ToString(CultureInfo.InvariantCulture),
        long l => l.ToString(CultureInfo.InvariantCulture),
        decimal m => m.ToString(CultureInfo.InvariantCulture),
        double d => d.ToString("R", CultureInfo.InvariantCulture),
        float f => f.ToString("R", CultureInfo.InvariantCulture),

        DateTimeOffset dto => RenderTimestampLiteral(dto),
        DateTime dt => RenderTimestampLiteral(NormalizeToUtc(dt)),

        // Anything else formattable (Guid, DateOnly, …) is quoted with an invariant ToString.
        IFormattable formattable => Quote(formattable.ToString(null, CultureInfo.InvariantCulture)),
        _ => Quote(value.ToString()!),
    };

    // A bare DateTime has no offset: UTC is lossless, Local is converted, Unspecified is taken as UTC (the
    // store's convention — every timestamp column defaults to UTC_TIMESTAMP/NOW and connects in UTC).
    private static DateTimeOffset NormalizeToUtc(DateTime dt) => dt.Kind switch
    {
        DateTimeKind.Utc => new DateTimeOffset(dt, TimeSpan.Zero),
        DateTimeKind.Local => new DateTimeOffset(dt).ToUniversalTime(),
        _ => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc), TimeSpan.Zero),
    };
}
