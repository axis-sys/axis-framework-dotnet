using AxisCache.Repository.Persistence;
using AxisCache.Repository.Ports;

namespace AxisCache.Postgres;

/// <summary>The single divergent statement for Postgres: an <c>ON CONFLICT</c> upsert casting the payload to JSONB.</summary>
internal sealed class PostgresCacheSqlDialect : IAxisCacheSqlDialect
{
    public string UpsertSql { get; } =
        $"""
         INSERT INTO {CacheEntriesTable.Table}
             ({CacheEntriesTable.CacheKey}, {CacheEntriesTable.ValueJson}, {CacheEntriesTable.ExpiresAt}, {CacheEntriesTable.UpdatedAt})
         VALUES (@key, @value::JSONB, @expiresAt, @updatedAt)
         ON CONFLICT ({CacheEntriesTable.CacheKey}) DO UPDATE SET
             {CacheEntriesTable.ValueJson} = EXCLUDED.{CacheEntriesTable.ValueJson},
             {CacheEntriesTable.ExpiresAt} = EXCLUDED.{CacheEntriesTable.ExpiresAt},
             {CacheEntriesTable.UpdatedAt} = EXCLUDED.{CacheEntriesTable.UpdatedAt}
         """;
}
