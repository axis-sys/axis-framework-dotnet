namespace Axis.Ddl;

/// <summary>
/// Per-dialect collation intent for a string column. Postgres compares case+accent-sensitively by default;
/// MySQL's server default (utf8mb4_0900_ai_ci) folds case AND accent. Pin the intent so the dialect can match
/// Postgres: <see cref="CaseAccentSensitive"/> for exact equality / unique keys (Postgres <c>=</c>),
/// <see cref="CaseInsensitiveAccentSensitive"/> for <c>ILIKE</c>/<c>lower()</c> search semantics.
/// </summary>
public enum AxisCollation
{
    Default,
    CaseAccentSensitive,
    CaseInsensitiveAccentSensitive,
}
