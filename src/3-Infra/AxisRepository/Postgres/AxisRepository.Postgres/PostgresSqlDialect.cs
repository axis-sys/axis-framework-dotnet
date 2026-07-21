using System.Globalization;
using Axis.Ddl;

namespace AxisRepository.Postgres;

/// <inheritdoc/>
public sealed class PostgresSqlDialect : AxisSqlDialectBase
{
    protected override string RenderType(AxisDbType dbType) => dbType switch
    {
        AxisDbType.VarcharType v => $"VARCHAR({v.Length})",
        AxisDbType.TextType => "TEXT",
        AxisDbType.IntType => "INT",
        AxisDbType.BoolType => "BOOLEAN",
        AxisDbType.JsonType => "JSONB",
        AxisDbType.TimestampUtcType => "TIMESTAMPTZ",
        AxisDbType.DecimalType d => $"NUMERIC({d.Precision},{d.Scale})",
        _ => throw new NotSupportedException($"Unsupported AxisType: {dbType}"),
    };

    protected override string RenderDefault(AxisDefault @default) => @default switch
    {
        AxisDefault.NowUtcDefault => "NOW()",
        AxisDefault.BoolDefault b => b.Value ? "TRUE" : "FALSE",
        AxisDefault.IntDefault i => i.Value.ToString(CultureInfo.InvariantCulture),
        AxisDefault.RawDefault r => r.Sql,
        _ => throw new NotSupportedException($"Unsupported AxisDefault: {@default}"),
    };

    protected override string RenderCheck(AxisCheck check, string column) => check switch
    {
        AxisCheck.IsTrueCheck => column,
        _ => throw new NotSupportedException($"Unsupported AxisCheck: {check}"),
    };

    protected override string RenderCollation(AxisCollation collation) => "";

    protected override string RenderBoolLiteral(bool value) => value ? "TRUE" : "FALSE";

    protected override string RenderSeedConflict(AxisSeed seed)
        => $"ON CONFLICT ({string.Join(", ", seed.ConflictColumns)}) DO NOTHING";

    protected override string RenderForeignKey(AxisForeignKey fk) => ForeignKeyConstraint(fk);

    // Pin the offset so the literal is UTC regardless of the session's TimeZone setting.
    protected override string RenderTimestampLiteral(DateTimeOffset utc) => $"TIMESTAMPTZ '{FormatUtcTimestamp(utc)}+00'";

    protected override IEnumerable<string> RenderInlineIndexLines(AxisTable table) => [];

    protected override IEnumerable<string> RenderPostTableStatements(AxisTable table)
        => table.Indexes.Select(idx =>
        {
            // 'unique' and 'where' already carry their own spacing when present, so they sit flush against the
            // surrounding tokens — no stray double spaces in the emitted DDL.
            var unique = idx.Unique ? "UNIQUE " : "";
            var where = idx.PartialPredicate is { } predicate ? $" WHERE {predicate}" : "";
            return $"CREATE {unique}INDEX IF NOT EXISTS {idx.Name} ON {table.Name} ({string.Join(", ", idx.Columns)}){where};";
        });
}
