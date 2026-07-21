using AxisBus.Postgres.Adapters;
using AxisBus.Repository;
using AxisBus.Repository.Ports;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AxisBus.Postgres;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the Postgres-backed durable outbox: the pooled data source and the Postgres INSERT dialect
    /// (the only dialect-specific code for the publish path), the schema bootstrap, the claim/mark-outcome
    /// dispatch store, and the dialect-agnostic core (<see cref="AxisBus.Repository.DependencyInjection.AddAxisBusRepositoryCore"/>).
    /// </summary>
    public static IServiceCollection AddAxisBusPostgres(this IServiceCollection services, AxisBusRepositorySettings settings)
    {
        if (services.Any(s => s.ServiceType == typeof(AxisBusRepositorySettings)))
            throw new InvalidOperationException(
                $"{nameof(AddAxisBusPostgres)} (or another AxisBus storage adapter) has already been registered. " +
                "AxisBus supports a single storage per process by design. Call this method exactly once during application startup.");

        services.AddSingleton<IAxisBusConnectionFactory>(_ => new PostgresBusConnectionFactory(NpgsqlDataSource.Create(settings.ConnectionString)));
        services.AddSingleton<IAxisBusSqlDialect, PostgresBusSqlDialect>();
        services.AddSingleton<IAxisBusStorageInitializer, PostgresBusStorageInitializer>();
        // Scoped, not Singleton: mirrors IBusEventStore — the dispatch store injects IAxisLogger<T>, which
        // depends on the scoped IAxisMediator (ambient request context).
        services.AddScoped<IBusEventDispatchStore, PostgresBusDispatchStore>();
        services.AddAxisBusRepositoryCore(settings);

        return services;
    }
}
