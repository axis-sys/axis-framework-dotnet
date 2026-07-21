using Axis;
using AxisCache.Repository.Ports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AxisCache.Repository;

/// <summary>
/// Background poll loop that actively reclaims expired L2 rows — the active twin of the passive drop done on
/// read (<see cref="ICacheEntryStore.GetAsync"/>), so a key that is never read again does not linger forever.
/// Registered only when <see cref="AxisCacheRepositorySettings.SweepEnabled"/> is set. Every
/// <see cref="AxisCacheRepositorySettings.SweepInterval"/> it opens a fresh <see cref="IServiceScope"/> and
/// runs <see cref="ICacheEntryStore.DeleteExpiredAsync"/> once; a failed pass is logged and retried on the
/// next tick rather than crashing the host — expiry is never lost, since reads still reclaim passively.
/// </summary>
/// <remarks>
/// Like <see cref="AxisCacheStorageInitializerWorker"/> this runs post-Build (honouring the host's resolved
/// connection string) and is opt-out. It does NOT bootstrap the schema — that is the initializer worker's
/// job. The store is Scoped (it injects <c>IAxisLogger&lt;T&gt;</c>, which depends on the scoped ambient
/// <c>IAxisMediator</c>), so it is resolved from a fresh per-sweep scope, never the root container.
/// </remarks>
internal sealed class AxisCacheSweepWorker(
    IServiceScopeFactory scopeFactory,
    AxisCacheRepositorySettings settings,
    TimeProvider timeProvider,
    ILogger<AxisCacheSweepWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<ICacheEntryStore>();
                var scopedLogger =
                    scope.ServiceProvider.GetRequiredService<IAxisLogger<AxisCacheSweepWorker>>();

                // The store never throws — a database failure surfaces as a failed AxisResult, which we log
                // and leave for the next tick to retry. Deleting nothing (0 rows) is a healthy success.
                var swept = await store.DeleteExpiredAsync(timeProvider.GetUtcNow());
                swept.LogIfFailure(scopedLogger, AxisFailureLogSeverity.Warning,
                    "Cache expiry sweep pass failed; retrying on the next tick.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning(ex, "Cache expiry sweep pass failed; retrying on the next tick."); }

            try { await Task.Delay(settings.SweepInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
