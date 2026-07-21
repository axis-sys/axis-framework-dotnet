namespace Axis.Ddl;

/// <summary>
/// A table-level <c>CHECK</c> constraint with a raw, dialect-portable boolean <see cref="Expression"/>
/// (e.g. a cross-column XOR). Rendered identically by every dialect as
/// <c>CONSTRAINT {Name} CHECK ({Expression})</c>, so the expression must use portable SQL.
/// </summary>
public sealed record AxisTableCheck(string Name, string Expression);
