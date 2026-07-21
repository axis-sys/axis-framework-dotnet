namespace Axis.Ddl;

/// <summary>
/// A foreign key. Every dialect renders it identically as a table-level named constraint
/// (<c>CONSTRAINT {Name} FOREIGN KEY (...) REFERENCES ...</c>); <see cref="OnDeleteCascade"/> appends
/// <c>ON DELETE CASCADE</c>.
/// </summary>
public sealed record AxisForeignKey(
    string Name,
    string Column,
    string ReferencedTable,
    string ReferencedColumn,
    bool OnDeleteCascade = false);
