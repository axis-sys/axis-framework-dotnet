using Axis;
using Axis.Persistence;
using Axis.Ports;
using Axis.Saga;
using AxisSaga.Postgres.Adapters;
using AxisSaga.Postgres.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AxisSaga.Postgres;

public static class DependencyInjection
{
    public static IServiceCollection AddAxisSagaPostgres(this IServiceCollection services, AxisSagaSettings settings)
    {
        if (services.Any(s => s.ServiceType == typeof(AxisSagaSettings)))
            throw new InvalidOperationException(
                $"{nameof(AddAxisSagaPostgres)} (or another AxisSaga storage adapter) has already been registered. " +
                "AxisSaga supports a single storage per process by design — all sagas across all BCs share the same AXIS_SAGA schema. " +
                "Call this method exactly once during application startup.");

        services.AddSingleton<AxisSagaPostgresDataSource>(_ => new AxisSagaPostgresDataSource(NpgsqlDataSource.Create(settings.ConnectionString)));

        // The agnostic settings store opens its connection through this seam — the wrapper IS the source.
        services.AddSingleton<IAxisSagaConnectionSource>(sp => sp.GetRequiredService<AxisSagaPostgresDataSource>());

        // Dialect-agnostic saga runtime (engine, mediator, resumer, janitor, definition initializer,
        // stage-handler invoker, event publisher, registry) + the hosted resumer worker.
        services.AddAxisSagaCore(settings);

        // Postgres storage ports — the only dialect-specific code.
        services.AddScoped<ISagaInstanceStore, SagaInstanceStore>();
        services.AddScoped<ISagaStageLogStore, SagaStageLogStore>();
        services.AddScoped<ISagaDefinitionStore, PostgresSagaDefinitionStore>();
        services.AddSingleton<IAxisSagaStorageInitializer, PostgresSagaStorageInitializer>();

        return services;
    }

    /// <summary>
    /// Keyed (per-subdomain) counterpart of <see cref="AddAxisSagaPostgres(IServiceCollection, AxisSagaSettings)"/>:
    /// registers a saga store keyed by <paramref name="serviceKey"/> — the same key convention as
    /// <c>AddPostgresUnitOfWork(serviceKey, connectionString)</c> — so independent subdomains each run their own
    /// saga against their own database in one process. When the BC already registered a keyed
    /// <see cref="NpgsqlDataSource"/> for this key (via <c>AddPostgresUnitOfWork</c>), the saga REUSES it (same
    /// pool); otherwise it creates and owns a saga-dedicated one from <see cref="AxisSagaSettings.ConnectionString"/>.
    /// Postgres defaults to READ COMMITTED, so the lease-claim concurrency is safe on the shared datasource.
    /// Call once per key. Coexists with the unkeyed overload and with other keys.
    /// </summary>
    public static IServiceCollection AddAxisSagaPostgres(this IServiceCollection services, string serviceKey, AxisSagaSettings settings)
    {
        if (string.IsNullOrWhiteSpace(serviceKey))
            throw new InvalidOperationException($"{nameof(AddAxisSagaPostgres)} requires a non-empty service key.");

        if (services.Any(s => s.ServiceType == typeof(AxisSagaSettings) && Equals(s.ServiceKey, serviceKey)))
            throw new InvalidOperationException(
                $"{nameof(AddAxisSagaPostgres)} (or another AxisSaga storage adapter) has already been registered for " +
                $"service key '{serviceKey}'. Register each subdomain's saga store exactly once, under its own key.");

        // Resolution-time detection (order-independent): reuse the repository's keyed datasource if present,
        // otherwise own a dedicated one. AxisSagaPostgresDataSource is a distinct wrapper type, so it never
        // collides with the repository's keyed NpgsqlDataSource registration.
        services.AddKeyedSingleton<AxisSagaPostgresDataSource>(serviceKey, (sp, key) =>
        {
            var repositoryDataSource = sp.GetKeyedService<NpgsqlDataSource>(key);
            return repositoryDataSource is not null
                ? new AxisSagaPostgresDataSource(repositoryDataSource, ownsInner: false)
                : new AxisSagaPostgresDataSource(NpgsqlDataSource.Create(settings.ConnectionString), ownsInner: true);
        });

        // The keyed agnostic settings store opens its connection through this keyed seam.
        services.AddKeyedSingleton<IAxisSagaConnectionSource>(serviceKey,
            (sp, key) => sp.GetRequiredKeyedService<AxisSagaPostgresDataSource>(key));

        // Keyed dialect-agnostic runtime (engine, mediator, resumer, janitor, definition initializer,
        // stage-handler invoker, event publisher, registry) + the per-key hosted resumer worker.
        services.AddAxisSagaCore(serviceKey, settings);

        // Postgres storage ports, keyed — each resolves the keyed AxisSagaPostgresDataSource.
        services.AddKeyedScoped<ISagaInstanceStore>(serviceKey, (sp, key) =>
            ActivatorUtilities.CreateInstance<SagaInstanceStore>(sp, sp.GetRequiredKeyedService<AxisSagaPostgresDataSource>(key)));
        services.AddKeyedScoped<ISagaStageLogStore>(serviceKey, (sp, key) =>
            ActivatorUtilities.CreateInstance<SagaStageLogStore>(sp, sp.GetRequiredKeyedService<AxisSagaPostgresDataSource>(key)));
        services.AddKeyedScoped<ISagaDefinitionStore>(serviceKey, (sp, key) =>
            ActivatorUtilities.CreateInstance<PostgresSagaDefinitionStore>(sp, sp.GetRequiredKeyedService<AxisSagaPostgresDataSource>(key)));
        services.AddKeyedSingleton<IAxisSagaStorageInitializer>(serviceKey, (sp, key) =>
            ActivatorUtilities.CreateInstance<PostgresSagaStorageInitializer>(sp, sp.GetRequiredKeyedService<AxisSagaSettings>(key)));

        return services;
    }
}
