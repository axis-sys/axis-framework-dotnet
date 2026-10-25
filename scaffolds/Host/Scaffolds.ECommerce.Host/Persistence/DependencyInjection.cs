using Axis.Saga;
using AxisBus.MySql;
using AxisBus.Postgres;
using AxisBus.Repository;
using AxisCache.MySql;
using AxisCache.Postgres;
using AxisCache.Repository;
using AxisSaga.MySql;
using AxisSaga.Postgres;
using Scaffolds.ECommerce.Adapters.Driven.MySql;
using Scaffolds.ECommerce.Adapters.Driven.Postgres;

namespace Scaffolds.ECommerce.Host.Persistence;

internal static class DependencyInjection
{
    internal static IServiceCollection AddECommercePersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var businessProvider = PersistenceConfiguration.Provider(configuration, PersistenceKeys.DatabaseProvider);
        var businessConnectionString = PersistenceConfiguration.ConnectionString(configuration, businessProvider);

        var sagaProvider = PersistenceConfiguration.Provider(configuration, PersistenceKeys.SagaProvider);
        var sagaPoolSize = PersistenceConfiguration.PositiveInt(configuration, PersistenceKeys.SagaMaxPoolSize);
        var sagaConnectionString = ConnectionPools.WithMaxPoolSize(
            sagaProvider, PersistenceConfiguration.ConnectionString(configuration, sagaProvider), sagaPoolSize);

        EnsurePoolsFitTheServer(configuration, businessProvider, businessConnectionString, sagaProvider, sagaPoolSize);

        return services
            .AddBusinessStore(configuration, businessProvider, businessConnectionString)
            .AddCacheStore(configuration, businessProvider, businessConnectionString)
            .AddSagaStore(configuration, sagaProvider, sagaConnectionString);
    }

    private static void EnsurePoolsFitTheServer(
        IConfiguration configuration,
        DatabaseProvider businessProvider,
        string businessConnectionString,
        DatabaseProvider sagaProvider,
        int sagaPoolSize)
    {
        var maxConnections = PersistenceConfiguration.PositiveInt(configuration, PersistenceKeys.DatabaseMaxConnections);
        var unitOfWorkPoolSize = ConnectionPools.MaxPoolSize(businessProvider, businessConnectionString);
        var outboxDispatcherPoolSize = unitOfWorkPoolSize;
        var cachePoolSize = unitOfWorkPoolSize;

        if (sagaProvider == businessProvider)
        {
            ConnectionPools.EnsureFitOneServer(maxConnections, unitOfWorkPoolSize, outboxDispatcherPoolSize, cachePoolSize, sagaPoolSize);
            return;
        }

        ConnectionPools.EnsureFitOneServer(maxConnections, unitOfWorkPoolSize, outboxDispatcherPoolSize, cachePoolSize);
        ConnectionPools.EnsureFitOneServer(maxConnections, sagaPoolSize);
    }

    private static IServiceCollection AddBusinessStore(
        this IServiceCollection services, IConfiguration configuration, DatabaseProvider provider, string connectionString)
    {
        var defaults = new AxisBusRepositorySettings { ConnectionString = connectionString };
        var bus = new AxisBusRepositorySettings
        {
            ConnectionString = connectionString,
            DispatcherEnabled = configuration.GetValue(PersistenceKeys.BusDispatcherEnabled, defaults.DispatcherEnabled),
            PollInterval = configuration.GetValue(PersistenceKeys.BusPollInterval, defaults.PollInterval),
        };

        return provider switch
        {
            DatabaseProvider.Postgres => services.AddECommercePostgres(configuration).AddAxisBusPostgres(bus),
            DatabaseProvider.MySql => services.AddECommerceMySql(configuration).AddAxisBusMySql(bus),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
        };
    }

    private static IServiceCollection AddCacheStore(
        this IServiceCollection services, IConfiguration configuration, DatabaseProvider provider, string connectionString)
    {
        var defaults = new AxisCacheRepositorySettings { ConnectionString = connectionString };
        var cache = new AxisCacheRepositorySettings
        {
            ConnectionString = connectionString,
            L1Ttl = configuration.GetValue(PersistenceKeys.CacheL1Ttl, defaults.L1Ttl),
            SweepEnabled = configuration.GetValue(PersistenceKeys.CacheSweepEnabled, defaults.SweepEnabled),
            SweepInterval = configuration.GetValue(PersistenceKeys.CacheSweepInterval, defaults.SweepInterval),
        };

        return provider switch
        {
            DatabaseProvider.Postgres => services.AddAxisCachePostgres(cache),
            DatabaseProvider.MySql => services.AddAxisCacheMySql(cache),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
        };
    }

    private static IServiceCollection AddSagaStore(
        this IServiceCollection services, IConfiguration configuration, DatabaseProvider provider, string connectionString)
    {
        var defaults = new AxisSagaSettings { ConnectionString = connectionString };
        var saga = new AxisSagaSettings
        {
            ConnectionString = connectionString,
            ResumerEnabled = configuration.GetValue(PersistenceKeys.SagaResumerEnabled, defaults.ResumerEnabled),
            JanitorEnabled = configuration.GetValue(PersistenceKeys.SagaJanitorEnabled, defaults.JanitorEnabled),
        };

        return provider switch
        {
            DatabaseProvider.Postgres => services.AddAxisSagaPostgres(saga),
            DatabaseProvider.MySql => services.AddAxisSagaMySql(saga),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
        };
    }
}
