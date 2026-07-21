using AxisCache.Repository.Persistence;
using AxisCache.Repository.Ports;

namespace AxisCache.MySql;

/// <summary>The single divergent statement for MySQL: an <c>ON DUPLICATE KEY</c> upsert (8.0.19+ row alias).</summary>
internal sealed class MySqlCacheSqlDialect : IAxisCacheSqlDialect
{
    public string UpsertSql { get; } =
        $"""
         INSERT INTO {CacheEntriesTable.Table}
             ({CacheEntriesTable.CacheKey}, {CacheEntriesTable.ValueJson}, {CacheEntriesTable.ExpiresAt}, {CacheEntriesTable.UpdatedAt})
         VALUES (@key, @value, @expiresAt, @updatedAt) AS new
         ON DUPLICATE KEY UPDATE
             {CacheEntriesTable.ValueJson} = new.{CacheEntriesTable.ValueJson},
             {CacheEntriesTable.ExpiresAt} = new.{CacheEntriesTable.ExpiresAt},
             {CacheEntriesTable.UpdatedAt} = new.{CacheEntriesTable.UpdatedAt}
         """;
}
