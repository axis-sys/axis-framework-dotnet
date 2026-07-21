using System.Globalization;
using Axis.Ddl;

namespace AxisRepository.MySql;

/// <inheritdoc/>
public sealed class MySqlSqlDialect : AxisSqlDialectBase
{
    protected override string RenderType(AxisDbType dbType) => dbType switch
    {
        AxisDbType.VarcharType v => $"VARCHAR({v.Length})",
        AxisDbType.TextType => "TEXT",
        AxisDbType.IntType => "INT",
        AxisDbType.BoolType => "TINYINT(1)",
        AxisDbType.JsonType => "JSON",
        AxisDbType.TimestampUtcType => "DATETIME(6)",
        AxisDbType.DecimalType d => $"DECIMAL({d.Precision},{d.Scale})",
        _ => throw new NotSupportedException($"Unsupported AxisType: {dbType}"),
    };

    protected override string RenderDefault(AxisDefault @default) => @default switch
    {
        AxisDefault.NowUtcDefault => "(UTC_TIMESTAMP(6))",
        AxisDefault.BoolDefault b => b.Value ? "1" : "0",
        AxisDefault.IntDefault i => i.Value.ToString(CultureInfo.InvariantCulture),
        AxisDefault.RawDefault r => r.Sql,
        _ => throw new NotSupportedException($"Unsupported AxisDefault: {@default}"),
    };

    protected override string RenderCheck(AxisCheck check, string column) => check switch
    {
        AxisCheck.IsTrueCheck => $"{column} = 1",
        _ => throw new NotSupportedException($"Unsupported AxisCheck: {check}"),
    };

    protected override string RenderCollation(AxisCollation collation) => collation switch
    {
        AxisCollation.CaseAccentSensitive => " COLLATE utf8mb4_0900_as_cs",
        AxisCollation.CaseInsensitiveAccentSensitive => " COLLATE utf8mb4_0900_as_ci",
        _ => "",
    };

    protected override string RenderBoolLiteral(bool value) => value ? "1" : "0";

    protected override string RenderSeedConflict(AxisSeed seed)
        => $"ON DUPLICATE KEY UPDATE {seed.ConflictColumns[0]} = {seed.ConflictColumns[0]}";

    protected override string RenderForeignKey(AxisForeignKey fk) => ForeignKeyConstraint(fk);

    // DATETIME(6) holds the UTC wall-clock (columns default to UTC_TIMESTAMP(6) and the connection is UTC).
    protected override string RenderTimestampLiteral(DateTimeOffset utc) => $"'{FormatUtcTimestamp(utc)}'";

    protected override IEnumerable<string> RenderPostTableStatements(AxisTable table) => [];

    protected override IEnumerable<string> RenderInlineIndexLines(AxisTable table)
    {
        foreach (var index in table.Indexes)
        {
            var cols = string.Join(", ", index.Columns);
            if (index is { Unique: true, PartialPredicate: { } predicate })
            {
                // Emulate a partial UNIQUE: a STORED-generated column holds the LAST index column only while
                // the predicate holds (NULL otherwise). MySQL ignores NULLs in a unique key, so rows outside
                // the predicate never collide — exactly the Postgres `WHERE {predicate}` semantics. The
                // predicate uses portable boolean equality (e.g. `IS_ACTIVE = TRUE`, read as TINYINT = 1 here).
                var conditionalColumn = index.Columns[^1];
                var source = table.Columns.First(c => c.Name == conditionalColumn);
                var generated = $"{index.Name}_KEY";
                var keyColumns = index.Columns.Count > 1
                    ? string.Join(", ", index.Columns.Take(index.Columns.Count - 1)) + ", " + generated
                    : generated;

                yield return $"{generated} {RenderType(source.DbType)}{RenderCollation(source.Collation)} "
                           + $"GENERATED ALWAYS AS (CASE WHEN {predicate} THEN {conditionalColumn} END) STORED";
                yield return $"UNIQUE KEY {index.Name} ({keyColumns})";
            }
            else if (index.Unique)
            {
                yield return $"UNIQUE KEY {index.Name} ({cols})";
            }
            else
            {
                yield return $"INDEX {index.Name} ({cols})";
            }
        }
    }
}
