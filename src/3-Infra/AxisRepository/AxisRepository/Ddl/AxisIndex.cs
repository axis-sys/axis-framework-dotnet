namespace Axis.Ddl;

/// <summary>
/// An index. <see cref="Unique"/> makes it a unique key;
/// <see cref="PartialPredicate"/> (a SQL boolean expression) makes it conditional:
/// - Postgres renders <c>WHERE {predicate}</c>;
/// - MySQL (no partial indexes) drops the predicate for a plain index, or emulates a partial UNIQUE with a generated column.
/// </summary>
public sealed record AxisIndex(
    string Name,
    IReadOnlyList<string> Columns,
    bool Unique = false,
    string? PartialPredicate = null);
