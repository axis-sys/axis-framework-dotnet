namespace Axis.Ddl;

/// <summary>
/// A <c>CHECK</c> on a column. <see cref="IsTrue"/> pins a boolean column to true (the single-row-guard pattern):
/// Postgres renders <c>CHECK (col)</c>, MySQL <c>CHECK (col = 1)</c>.
/// </summary>
public abstract record AxisCheck
{
    private AxisCheck() { }

    public sealed record IsTrueCheck : AxisCheck;
    public static AxisCheck IsTrue { get; } = new IsTrueCheck();
}
