using Axis.Ddl;

namespace AxisCache.Repository.Persistence;

/// <summary>
/// The single L2 cache table, declared ONCE and dialect-agnostic. An injected <see cref="IAxisSqlDialect"/>
/// (Postgres or MySQL) renders the concrete DDL, so column names live in one place and the two adapters
/// differ only by dialect.
/// </summary>
internal static class CacheEntriesTable
{
    public const string Schema = "AXIS_CACHE";

    public const string Table = $"{Schema}.CACHE_ENTRIES";

    public const string CacheKey = "CACHE_KEY";
    public const string ValueJson = "VALUE_JSON";
    public const string ExpiresAt = "EXPIRES_AT";
    public const string UpdatedAt = "UPDATED_AT";

    // EXPIRES_AT nullable = never expires (invalidated explicitly). The index keeps the janitor sweep of
    // expired rows selective. Expiry is always evaluated against a caller-supplied UTC instant, never the
    // database clock, so the comparison is identical on Postgres and MySQL regardless of server timezone.
    public static AxisTable Define() => new AxisTable(Table)
        .Column(CacheKey, AxisDbType.Varchar(200), primaryKey: true)
        .Column(ValueJson, AxisDbType.Json, notNull: true)
        .Column(ExpiresAt, AxisDbType.TimestampUtc)
        .Column(UpdatedAt, AxisDbType.TimestampUtc, notNull: true, @default: AxisDefault.NowUtc)
        .Index("IDX_CACHE_ENTRIES_EXPIRES_AT", ExpiresAt);
}
