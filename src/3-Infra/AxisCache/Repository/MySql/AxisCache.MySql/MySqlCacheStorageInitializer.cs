using AxisCache.MySql.Persistence;
using AxisCache.Repository;
using AxisCache.Repository.Ports;

namespace AxisCache.MySql;

internal sealed class MySqlCacheStorageInitializer(AxisCacheRepositorySettings settings) : IAxisCacheStorageInitializer
{
    public Task InitializeAsync() => AxisCacheMigrations.InitializeMySqlAsync(settings.ConnectionString);
}
