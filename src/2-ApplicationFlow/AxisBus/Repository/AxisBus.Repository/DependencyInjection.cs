using Axis;
using AxisBus.Repository.Outbox;
using AxisBus.Repository.Ports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AxisBus.Repository;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the dialect-agnostic atomic-outbox runtime: the request-scoped enqueue queue, the
    /// <see cref="IAxisBus"/> write-path adapter, the <see cref="IAxisRepositoryOutbox"/> drain (which the unit
    /// of work invokes at commit), and the read/dispatch path (<see cref="IBusDispatcher"/> + its poll worker).
    /// A storage adapter (<c>AddAxisBusPostgres</c> / <c>AddAxisBusMySql</c>) calls this once and supplies the
    /// dialect seams (<see cref="IAxisBusConnectionFactory"/>, <see cref="IAxisBusSqlDialect"/>) plus
    /// <see cref="IBusEventDispatchStore"/>.
    /// </summary>
    public static IServiceCollection AddAxisBusRepositoryCore(this IServiceCollection services, AxisBusRepositorySettings settings)
    {
        services.AddSingleton(settings);
        services.TryAddSingleton(TimeProvider.System);

        // The request-scoped queue: publishing enqueues here, the drain flushes it at commit.
        services.AddScoped<IOutboxScopedQueue, OutboxScopedQueue>();

        // Write path. Scoped, not Singleton: the drain reads the ambient IAxisMediator via IAxisLogger<T>, the
        // same reason the AxisCache L2 store and the AxisSaga stores are scoped.
        services.AddScoped<IAxisBus, RepositoryBusAdapter>();

        // The bridge the unit of work drains at commit. RemoveAll + Add so this replaces the
        // NullAxisRepositoryOutbox default (registered by AddPostgres/MySqlUnitOfWork) regardless of the order
        // the two AddAxis… calls run in.
        services.RemoveAll<IAxisRepositoryOutbox>();
        services.AddScoped<IAxisRepositoryOutbox, RepositoryOutboxDrain>();

        // Scoped for the same reason as the write path; the worker resolves it from its own fresh per-pass scope.
        services.AddScoped<IBusDispatcher, BusDispatcher>();

        // Host the one-shot schema bootstrap here so consumers get it for free. Opt out via RunStartupMigration.
        if (settings.RunStartupMigration)
            services.AddHostedService<AxisBusStorageInitializerWorker>();

        // Opt out via DispatcherEnabled on hosts that only publish and must not drain the outbox themselves.
        if (settings.DispatcherEnabled)
            services.AddHostedService<AxisBusDispatcherWorker>();

        return services;
    }
}
