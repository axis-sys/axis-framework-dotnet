namespace Axis.Ddl;

/// <summary>
/// An idempotent seed: <see cref="Rows"/> inserted at migration time, made a no-op on a conflict of <see cref="ConflictColumns"/>
/// (Postgres <c>ON CONFLICT (...) DO NOTHING</c>,
/// MySQL <c>ON DUPLICATE KEY UPDATE col = col</c> — NOT <c>INSERT IGNORE</c>,
/// which would also swallow FK/NOT NULL errors).
/// </summary>
public sealed record AxisSeed(
    IReadOnlyList<string> Columns,
    IReadOnlyList<string> ConflictColumns,
    IReadOnlyList<IReadOnlyList<object?>> Rows);
