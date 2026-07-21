using AxisCache.Repository.Ports;
using Microsoft.Extensions.Hosting;

namespace AxisCache.Repository;

/// <summary>
/// Creates the L2 schema once on startup via the dialect <see cref="IAxisCacheStorageInitializer"/> — the
/// cache twin of the saga's storage bootstrap. Registered only when
/// <see cref="AxisCacheRepositorySettings.RunStartupMigration"/> is set. Runs post-Build, so it honours the
/// host's resolved connection string; idempotent, so a fixture that migrated first makes it a no-op.
/// </summary>
internal sealed class AxisCacheStorageInitializerWorker(IAxisCacheStorageInitializer initializer) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => initializer.InitializeAsync();
}
