using Axis.Ports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Axis.Saga;

/// <summary>
/// Background resumer hosted by the saga storage adapter, so consumers no longer hand-roll one per
/// application. On startup it migrates the saga schema (via the dialect <see cref="IAxisSagaStorageInitializer"/>)
/// and upserts the in-process saga definitions, then polls <see cref="IAxisSagaResumer.RunOnceAsync"/>
/// every <see cref="AxisSagaSettings.ResumerPollInterval"/> to reclaim and re-fire stale instances.
/// Dialect-agnostic: it depends only on the storage-initializer and resumer ports.
/// </summary>
internal sealed class AxisSagaResumerWorker(
    IServiceScopeFactory scopeFactory,
    IAxisSagaStorageInitializer storageInitializer,
    AxisSagaSettings settings,
    ILogger<AxisSagaResumerWorker> logger,
    // Non-null only on the keyed (per-subdomain) registration: the scoped resumer and definition
    // initializer must be resolved for THIS store's key. Null preserves the single-store behaviour.
    // The ctor-injected storageInitializer/settings are already the keyed ones (supplied by the keyed
    // registration factory), so only the per-scope resolutions below need the key.
    string? serviceKey = null
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Idempotent: applied versions are skipped, so this is a no-op when the schema is already
        // migrated (e.g. a test fixture ran it first). Runs post-Build, so it honours the host's
        // resolved connection string.
        await storageInitializer.InitializeAsync();

        var definitionsInitialized = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                if (!definitionsInitialized)
                {
                    var initializer = serviceKey is null
                        ? scope.ServiceProvider.GetRequiredService<IAxisSagaDefinitionInitializer>()
                        : scope.ServiceProvider.GetRequiredKeyedService<IAxisSagaDefinitionInitializer>(serviceKey);
                    await initializer.InitializeAsync(stoppingToken);
                    definitionsInitialized = true;
                }

                var resumer = serviceKey is null
                    ? scope.ServiceProvider.GetRequiredService<IAxisSagaResumer>()
                    : scope.ServiceProvider.GetRequiredKeyedService<IAxisSagaResumer>(serviceKey);
                await resumer.RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning(ex, "Saga resumer pass failed; retrying on the next poll."); }

            try { await Task.Delay(settings.ResumerPollInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
