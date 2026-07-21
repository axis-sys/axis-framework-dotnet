namespace Axis.Ddl;

/// <summary>One column of an <see cref="AxisTable"/>.</summary>
public sealed record AxisColumn(
    string Name,
    AxisDbType DbType,
    bool NotNull = false,
    AxisDefault? Default = null,
    bool PrimaryKey = false,
    AxisCheck? Check = null,
    AxisCollation Collation = AxisCollation.Default);
