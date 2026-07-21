namespace Axis.Ddl;

/// <summary>A column <c>DEFAULT</c>, rendered per dialect (<see cref="NowUtc"/> → <c>NOW()</c> / <c>UTC_TIMESTAMP(6)</c>).</summary>
public abstract record AxisDefault
{
    private AxisDefault() { }

    public sealed record NowUtcDefault : AxisDefault;
    public sealed record BoolDefault(bool Value) : AxisDefault;
    public sealed record IntDefault(int Value) : AxisDefault;
    public sealed record RawDefault(string Sql) : AxisDefault;

    public static AxisDefault NowUtc { get; } = new NowUtcDefault();
    public static AxisDefault Bool(bool value) => new BoolDefault(value);
    public static AxisDefault Int(int value) => new IntDefault(value);
    public static AxisDefault Raw(string sql) => new RawDefault(sql);
}
