using Axis.Saga;
using AxisBus.Repository;
using AxisCache.Repository;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Scaffolds.ECommerce.Host.Persistence;
using MySqlProvider = Scaffolds.ECommerce.Adapters.Driven.MySql.DependencyInjection;
using PostgresProvider = Scaffolds.ECommerce.Adapters.Driven.Postgres.DependencyInjection;

namespace Scaffolds.ECommerce.E2ETests;

public sealed class PersistenceCompositionTests
{
    [Fact]
    public void FallsBackToPostgresWhenTheProviderSwitchIsUnset()
    {
        var configuration = Configuration(ComposableSettings());

        var provider = PersistenceConfiguration.Provider(configuration, PersistenceKeys.SagaProvider);

        Assert.Equal(DatabaseProvider.Postgres, provider);
    }

    [Fact]
    public void MatchesTheProviderSwitchWhenItsCaseDiffers()
    {
        var settings = ComposableSettings();
        settings[PersistenceKeys.SagaProvider] = "mYsQl";

        var provider = PersistenceConfiguration.Provider(Configuration(settings), PersistenceKeys.SagaProvider);

        Assert.Equal(DatabaseProvider.MySql, provider);
    }

    [Fact]
    public void RefusesToComposeWhenTheSagaProviderIsUnknown()
    {
        var settings = ComposableSettings();
        settings[PersistenceKeys.SagaProvider] = "Oracle";

        var compose = () => new ServiceCollection().AddECommercePersistence(Configuration(settings));

        Assert.Throws<InvalidOperationException>(compose);
    }

    [Fact]
    public void RefusesToComposeWhenTheSagaProviderHasNoConnectionString()
    {
        var settings = ComposableSettings();
        settings[PersistenceKeys.SagaProvider] = nameof(DatabaseProvider.MySql);
        settings.Remove($"ConnectionStrings:{MySqlProvider.ConnectionStringName}");

        var compose = () => new ServiceCollection().AddECommercePersistence(Configuration(settings));

        Assert.Throws<InvalidOperationException>(compose);
    }

    [Fact]
    public void RefusesToComposeWhenTheSagaPoolSizeIsMissing()
    {
        var settings = ComposableSettings();
        settings.Remove(PersistenceKeys.SagaMaxPoolSize);

        var compose = () => new ServiceCollection().AddECommercePersistence(Configuration(settings));

        Assert.Throws<InvalidOperationException>(compose);
    }

    [Fact]
    public void RefusesToComposeWhenThePoolsSharingOneServerExceedItsConnectionBudget()
    {
        var settings = ComposableSettings();
        settings[PersistenceKeys.DatabaseMaxConnections] = "45";

        var compose = () => new ServiceCollection().AddECommercePersistence(Configuration(settings));

        Assert.Throws<InvalidOperationException>(compose);
    }

    [Fact]
    public void SizesTheSagaPoolFromItsOwnKeyWhenComposed()
    {
        var services = new ServiceCollection().AddECommercePersistence(Configuration(ComposableSettings()));

        var saga = Registered<AxisSagaSettings>(services);
        var bus = Registered<AxisBusRepositorySettings>(services);

        Assert.Equal(10, new NpgsqlConnectionStringBuilder(saga.ConnectionString).MaxPoolSize);
        Assert.Equal(20, new NpgsqlConnectionStringBuilder(bus.ConnectionString).MaxPoolSize);
    }

    [Fact]
    public void ReadsTheWorkerTogglesFromConfigurationWhenTheyAreSet()
    {
        var settings = ComposableSettings();
        settings[PersistenceKeys.SagaResumerEnabled] = "false";
        settings[PersistenceKeys.SagaJanitorEnabled] = "false";
        settings[PersistenceKeys.BusDispatcherEnabled] = "false";
        settings[PersistenceKeys.BusPollInterval] = "00:00:42";

        var services = new ServiceCollection().AddECommercePersistence(Configuration(settings));

        var saga = Registered<AxisSagaSettings>(services);
        var bus = Registered<AxisBusRepositorySettings>(services);
        Assert.False(saga.ResumerEnabled);
        Assert.False(saga.JanitorEnabled);
        Assert.False(bus.DispatcherEnabled);
        Assert.Equal(TimeSpan.FromSeconds(42), bus.PollInterval);
    }

    [Fact]
    public void KeepsTheFrameworkWorkerDefaultsWhenTheTogglesAreUnset()
    {
        var services = new ServiceCollection().AddECommercePersistence(Configuration(ComposableSettings()));

        var saga = Registered<AxisSagaSettings>(services);
        var bus = Registered<AxisBusRepositorySettings>(services);
        var sagaDefaults = new AxisSagaSettings { ConnectionString = saga.ConnectionString };
        var busDefaults = new AxisBusRepositorySettings { ConnectionString = bus.ConnectionString };
        Assert.Equal(sagaDefaults.ResumerEnabled, saga.ResumerEnabled);
        Assert.Equal(sagaDefaults.JanitorEnabled, saga.JanitorEnabled);
        Assert.Equal(busDefaults.DispatcherEnabled, bus.DispatcherEnabled);
        Assert.Equal(busDefaults.PollInterval, bus.PollInterval);
    }

    [Fact]
    public void ReadsTheCacheSettingsFromConfigurationWhenTheyAreSet()
    {
        var settings = ComposableSettings();
        settings[PersistenceKeys.CacheL1Ttl] = "00:00:07";
        settings[PersistenceKeys.CacheSweepEnabled] = "false";
        settings[PersistenceKeys.CacheSweepInterval] = "00:03:00";

        var services = new ServiceCollection().AddECommercePersistence(Configuration(settings));

        var cache = Registered<AxisCacheRepositorySettings>(services);
        Assert.Equal(TimeSpan.FromSeconds(7), cache.L1Ttl);
        Assert.False(cache.SweepEnabled);
        Assert.Equal(TimeSpan.FromMinutes(3), cache.SweepInterval);
    }

    [Fact]
    public void KeepsTheCacheDefaultsOnTheBusinessStoreWhenTheCacheSettingsAreUnset()
    {
        var services = new ServiceCollection().AddECommercePersistence(Configuration(ComposableSettings()));

        var cache = Registered<AxisCacheRepositorySettings>(services);
        var defaults = new AxisCacheRepositorySettings { ConnectionString = cache.ConnectionString };
        Assert.Equal(20, new NpgsqlConnectionStringBuilder(cache.ConnectionString).MaxPoolSize);
        Assert.Equal(defaults.L1Ttl, cache.L1Ttl);
        Assert.Equal(defaults.SweepEnabled, cache.SweepEnabled);
        Assert.Equal(defaults.SweepInterval, cache.SweepInterval);
    }

    [Fact]
    public void CountsTheCachePoolInTheConnectionBudgetWhenComposed()
    {
        var settings = ComposableSettings();
        settings[PersistenceKeys.DatabaseMaxConnections] = "65";

        var compose = () => new ServiceCollection().AddECommercePersistence(Configuration(settings));

        Assert.Throws<InvalidOperationException>(compose);
    }

    private static Dictionary<string, string?> ComposableSettings() => new()
    {
        [$"ConnectionStrings:{PostgresProvider.ConnectionStringName}"] = "Host=localhost;Database=ecommerce;Username=app;Maximum Pool Size=20",
        [$"ConnectionStrings:{MySqlProvider.ConnectionStringName}"] = "Server=localhost;Database=ecommerce;User ID=app;Maximum Pool Size=20",
        [PersistenceKeys.DatabaseMaxConnections] = "100",
        [PersistenceKeys.SagaMaxPoolSize] = "10",
    };

    private static IConfiguration Configuration(Dictionary<string, string?> settings)
        => new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    private static T Registered<T>(IServiceCollection services)
        => Assert.IsType<T>(services.Single(descriptor => descriptor.ServiceType == typeof(T)).ImplementationInstance);
}
