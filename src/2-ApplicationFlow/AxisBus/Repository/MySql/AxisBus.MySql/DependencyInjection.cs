using AxisBus.MySql.Adapters;
using AxisBus.Repository;
using AxisBus.Repository.Ports;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;

namespace AxisBus.MySql;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the MySQL-backed durable outbox: the pinned pooled data source, the MySQL insert dialect,
    /// the schema initializer and the dispatch-side store (claim/mark-outcome/retention), plus the
    /// dialect-agnostic core (<c>IBusEventStore</c>, <c>IAxisBus</c>) via
    /// <see cref="AxisBus.Repository.DependencyInjection.AddAxisBusRepositoryCore"/> in <c>AxisBus.Repository</c>.
    /// </summary>
    public static IServiceCollection AddAxisBusMySql(this IServiceCollection services, AxisBusRepositorySettings settings)
    {
        if (services.Any(s => s.ServiceType == typeof(AxisBusRepositorySettings)))
            throw new InvalidOperationException(
                $"{nameof(AddAxisBusMySql)} (or another AxisBus storage adapter) has already been registered. " +
                "AxisBus supports a single storage per process by design. Call this method exactly once during application startup.");

        services.AddSingleton<IAxisBusConnectionFactory>(_ => new MySqlBusConnectionFactory(BuildDataSource(settings.ConnectionString)));
        services.AddSingleton<IAxisBusSqlDialect, MySqlBusSqlDialect>();
        services.AddSingleton<IAxisBusStorageInitializer, MySqlBusStorageInitializer>();

        // Scoped, not Singleton: MySqlBusDispatchStore injects IAxisLogger<T>, which depends on the scoped
        // IAxisMediator ambient context — the same reason IBusEventStore/IAxisBus are scoped in the core.
        services.AddScoped<IBusEventDispatchStore, MySqlBusDispatchStore>();

        services.AddAxisBusRepositoryCore(settings);

        return services;
    }

    // Every dispatch-store connection is pinned to READ COMMITTED. ClaimBatchAsync's per-row claim UPDATE
    // (WHERE EVENT_ID = @id AND ...) is a single-PK write with no range scan, so it does not itself need the
    // looser isolation — but the discovery SELECT and the read-back SELECT run on the same pooled data
    // source as the saga/cache adapters that DO need it, and mixing isolation levels across connections
    // pulled from one pool invites the exact deadlock class AxisSaga.MySql already hit in production.
    // Pinning uniformly here mirrors AxisSaga.MySql.DependencyInjection.BuildDataSource. The SET runs only
    // on brand-new physical connections; the session setting persists across pool reuse.
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
