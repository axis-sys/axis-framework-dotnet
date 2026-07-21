namespace Axis.Ddl;

/// <summary>
/// Logical, dialect-agnostic column type. An <see cref="IAxisSqlDialect"/> renders each to the concrete SQL
/// type — e.g. <see cref="Bool"/> → <c>BOOLEAN</c> (Postgres) / <c>TINYINT(1)</c> (MySQL), <see cref="Json"/>
/// → <c>JSONB</c> / <c>JSON</c>, <see cref="TimestampUtc"/> → <c>TIMESTAMPTZ</c> / <c>DATETIME(6)</c>.
/// </summary>
public abstract record AxisDbType
{
    private AxisDbType() { }

    public sealed record VarcharType(int Length) : AxisDbType;
    public sealed record TextType : AxisDbType;
    public sealed record IntType : AxisDbType;
    public sealed record BoolType : AxisDbType;
    public sealed record JsonType : AxisDbType;
    public sealed record TimestampUtcType : AxisDbType;
    public sealed record DecimalType(int Precision, int Scale) : AxisDbType;

    public static AxisDbType Varchar(int length) => new VarcharType(length);
    public static AxisDbType Text { get; } = new TextType();
    public static AxisDbType Int { get; } = new IntType();
    public static AxisDbType Bool { get; } = new BoolType();
    public static AxisDbType Json { get; } = new JsonType();
    public static AxisDbType TimestampUtc { get; } = new TimestampUtcType();
    public static AxisDbType Decimal(int precision, int scale) => new DecimalType(precision, scale);
}
