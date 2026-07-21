using AxisCache.Postgres.Persistence;
using AxisCache.Repository;
using AxisCache.Repository.Ports;

namespace AxisCache.Postgres;

internal sealed class PostgresCacheStorageInitializer(AxisCacheRepositorySettings settings) : IAxisCacheStorageInitializer
{
    public Task InitializeAsync() => AxisCacheMigrations.InitializePostgresAsync(settings.ConnectionString);
}
