using Axis;
using AxisCache.Repository;
using AxisCache.Repository.Ports;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AxisCache.Postgres;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the Postgres-backed two-tier <see cref="IAxisCache"/>: the pooled data source and the
    /// Postgres upsert dialect (the only dialect-specific code) plus the dialect-agnostic core. The L2
    /// schema is created out of band via <see cref="Persistence.AxisCacheMigrations.InitializePostgresAsync"/>.
    /// </summary>
    public static IServiceCollection AddAxisCachePostgres(this IServiceCollection services, AxisCacheRepositorySettings settings)
    {
        if (services.Any(s => s.ServiceType == typeof(AxisCacheRepositorySettings)))
            throw new InvalidOperationException(
                $"{nameof(AddAxisCachePostgres)} (or another AxisCache storage adapter) has already been registered. " +
                "AxisCache supports a single storage per process by design. Call this method exactly once during application startup.");

        services.AddSingleton<IAxisCacheConnectionFactory>(_ => new PostgresCacheConnectionFactory(NpgsqlDataSource.Create(settings.ConnectionString)));
        services.AddSingleton<IAxisCacheSqlDialect, PostgresCacheSqlDialect>();
        services.AddSingleton<IAxisCacheStorageInitializer, PostgresCacheStorageInitializer>();
        services.AddAxisCacheRepositoryCore(settings);

        return services;
    }
}
