using Axis;
using Axis.Persistence;
using Axis.Ports;
using Axis.Saga;
using AxisSaga.MySql.Adapters;
using AxisSaga.MySql.Persistence;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;

namespace AxisSaga.MySql;

public static class DependencyInjection
{
    public static IServiceCollection AddAxisSagaMySql(this IServiceCollection services, AxisSagaSettings settings)
    {
        if (services.Any(s => s.ServiceType == typeof(AxisSagaSettings)))
            throw new InvalidOperationException(
                $"{nameof(AddAxisSagaMySql)} (or another AxisSaga storage adapter) has already been registered. " +
                "AxisSaga supports a single storage per process by design — all sagas across all BCs share the same AXIS_SAGA schema. " +
                "Call this method exactly once during application startup.");

        services.AddSingleton<AxisSagaMySqlDataSource>(_ => new AxisSagaMySqlDataSource(BuildDataSource(settings.ConnectionString)));

        // The agnostic settings store opens its connection through this seam — the wrapper IS the source.
        services.AddSingleton<IAxisSagaConnectionSource>(sp => sp.GetRequiredService<AxisSagaMySqlDataSource>());

        // Dialect-agnostic saga runtime (engine, mediator, resumer, janitor, definition initializer,
        // stage-handler invoker, event publisher, registry) + the hosted resumer worker.
        services.AddAxisSagaCore(settings);

        // MySQL storage ports — the only dialect-specific code.
        services.AddScoped<ISagaInstanceStore, MySqlSagaInstanceStore>();
        services.AddScoped<ISagaStageLogStore, MySqlSagaStageLogStore>();
        services.AddScoped<ISagaDefinitionStore, MySqlSagaDefinitionStore>();
        services.AddSingleton<IAxisSagaStorageInitializer, MySqlSagaStorageInitializer>();

        return services;
    }

    /// <summary>
    /// Keyed (per-subdomain) counterpart of <see cref="AddAxisSagaMySql(IServiceCollection, AxisSagaSettings)"/>:
    /// registers a saga store keyed by <paramref name="serviceKey"/> — the same key convention as
    /// <c>AddMySqlUnitOfWork(serviceKey, connectionString)</c> — so independent subdomains each run their own saga
    /// against their own database in one process. Unlike the Postgres adapter, the MySQL saga ALWAYS builds and owns
    /// its own datasource: <see cref="BuildDataSource"/> pins new connections to READ COMMITTED (the lease claim
    /// gap-locks under InnoDB's default REPEATABLE READ), which the repository's plain keyed <c>MySqlDataSource</c>
    /// does not do — so it cannot be reused. Call once per key. Coexists with the unkeyed overload and with other keys.
    /// </summary>
    public static IServiceCollection AddAxisSagaMySql(this IServiceCollection services, string serviceKey, AxisSagaSettings settings)
    {
        if (string.IsNullOrWhiteSpace(serviceKey))
            throw new InvalidOperationException($"{nameof(AddAxisSagaMySql)} requires a non-empty service key.");

        if (services.Any(s => s.ServiceType == typeof(AxisSagaSettings) && Equals(s.ServiceKey, serviceKey)))
            throw new InvalidOperationException(
                $"{nameof(AddAxisSagaMySql)} (or another AxisSaga storage adapter) has already been registered for " +
                $"service key '{serviceKey}'. Register each subdomain's saga store exactly once, under its own key.");

        services.AddKeyedSingleton<AxisSagaMySqlDataSource>(serviceKey,
            (_, _) => new AxisSagaMySqlDataSource(BuildDataSource(settings.ConnectionString)));

        // The keyed agnostic settings store opens its connection through this keyed seam.
        services.AddKeyedSingleton<IAxisSagaConnectionSource>(serviceKey,
            (sp, key) => sp.GetRequiredKeyedService<AxisSagaMySqlDataSource>(key));

        // Keyed dialect-agnostic runtime + the per-key hosted resumer worker.
        services.AddAxisSagaCore(serviceKey, settings);

        // MySQL storage ports, keyed — each resolves the keyed AxisSagaMySqlDataSource.
        services.AddKeyedScoped<ISagaInstanceStore>(serviceKey, (sp, key) =>
            ActivatorUtilities.CreateInstance<MySqlSagaInstanceStore>(sp, sp.GetRequiredKeyedService<AxisSagaMySqlDataSource>(key)));
        services.AddKeyedScoped<ISagaStageLogStore>(serviceKey, (sp, key) =>
            ActivatorUtilities.CreateInstance<MySqlSagaStageLogStore>(sp, sp.GetRequiredKeyedService<AxisSagaMySqlDataSource>(key)));
        services.AddKeyedScoped<ISagaDefinitionStore>(serviceKey, (sp, key) =>
            ActivatorUtilities.CreateInstance<MySqlSagaDefinitionStore>(sp, sp.GetRequiredKeyedService<AxisSagaMySqlDataSource>(key)));
        services.AddKeyedSingleton<IAxisSagaStorageInitializer>(serviceKey, (sp, key) =>
            ActivatorUtilities.CreateInstance<MySqlSagaStorageInitializer>(sp, sp.GetRequiredKeyedService<AxisSagaSettings>(key)));

        return services;
    }

    // The store pins every saga-store connection to READ COMMITTED. The lease claim
    // (MySqlSagaInstanceStore.AcquireLeaseAsync) gates on a COUNT over SAGA_INSTANCES inside the
    // UPDATE; under InnoDB's default REPEATABLE READ that scan takes next-key/gap locks, which
    // deadlock against concurrent claims and block concurrent INSERTs under the import fan-out.
    // READ COMMITTED drops the gap locks. The global cap is already a soft cap by design, so the
    // looser isolation does not change its semantics. The SET runs only on brand-new physical
    // connections; the session setting persists across pool reuse.
    private static MySqlDataSource BuildDataSource(string connectionString)
    {
        MySqlDataSourceBuilder builder = new(connectionString);
        builder.UseConnectionOpenedCallback(async (context, cancellationToken) =>
        {
            if ((context.Conditions & MySqlConnectionOpenedConditions.New) == 0)
                return;

            await using var cmd = context.Connection.CreateCommand();
            cmd.CommandText = "SET SESSION TRANSACTION ISOLATION LEVEL READ COMMITTED";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        });
        return builder.Build();
    }
}
