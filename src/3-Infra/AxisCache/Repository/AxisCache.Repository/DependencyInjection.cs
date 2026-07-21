using Axis;
using AxisCache.Repository.Ports;
using AxisCache.Repository.Store;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AxisCache.Repository;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the dialect-agnostic two-tier cache runtime (the L1 memory cache, the shared L2 store and
    /// the <see cref="IAxisCache"/> adapter). A storage adapter (<c>AddAxisCachePostgres</c> /
    /// <c>AddAxisCacheMySql</c>) calls this once and supplies the two dialect seams
    /// (<see cref="IAxisCacheConnectionFactory"/>, <see cref="IAxisCacheSqlDialect"/>).
    /// </summary>
    public static IServiceCollection AddAxisCacheRepositoryCore(this IServiceCollection services, AxisCacheRepositorySettings settings)
    {
        services.AddSingleton(settings);
        services.AddMemoryCache();
        services.TryAddSingleton(TimeProvider.System);
        // Scoped, not Singleton: the store injects IAxisLogger<T>, which depends on the scoped IAxisMediator
        // (ambient request context) — the same reason the AxisSaga stores are scoped. L1 is the shared
        // singleton IMemoryCache, so caching still spans requests.
        services.AddScoped<ICacheEntryStore, CacheEntryStore>();
        services.AddScoped<IAxisCache, RepositoryCacheAdapter>();

        // Host the one-shot schema bootstrap here so consumers get it for free. Opt out via
        // RunStartupMigration on hosts that must not touch the database at boot (e.g. faked-port test hosts).
        if (settings.RunStartupMigration)
            services.AddHostedService<AxisCacheStorageInitializerWorker>();

        // Host the periodic expiry sweep here too. Opt out via SweepEnabled on hosts that must not run
        // background database work, or that delegate the sweep to a dedicated process — expiry is still
        // reclaimed passively on read either way.
        if (settings.SweepEnabled)
            services.AddHostedService<AxisCacheSweepWorker>();

        return services;
    }
}
