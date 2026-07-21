using Axis;
using AxisCache.Repository;
using AxisCache.Repository.Ports;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;

namespace AxisCache.MySql;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the MySQL-backed two-tier <see cref="IAxisCache"/>: the pooled data source and the MySQL
    /// upsert dialect (the only dialect-specific code) plus the dialect-agnostic core. The L2 schema is
    /// created out of band via <see cref="Persistence.AxisCacheMigrations.InitializeMySqlAsync"/>.
    /// </summary>
    public static IServiceCollection AddAxisCacheMySql(this IServiceCollection services, AxisCacheRepositorySettings settings)
    {
        if (services.Any(s => s.ServiceType == typeof(AxisCacheRepositorySettings)))
            throw new InvalidOperationException(
                $"{nameof(AddAxisCacheMySql)} (or another AxisCache storage adapter) has already been registered. " +
                "AxisCache supports a single storage per process by design. Call this method exactly once during application startup.");

        services.AddSingleton<IAxisCacheConnectionFactory>(_ => new MySqlCacheConnectionFactory(new MySqlDataSource(settings.ConnectionString)));
        services.AddSingleton<IAxisCacheSqlDialect, MySqlCacheSqlDialect>();
        services.AddSingleton<IAxisCacheStorageInitializer, MySqlCacheStorageInitializer>();
        services.AddAxisCacheRepositoryCore(settings);

        return services;
    }
}
