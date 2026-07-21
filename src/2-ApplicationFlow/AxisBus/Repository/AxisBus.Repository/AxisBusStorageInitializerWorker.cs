using AxisBus.Repository.Ports;
using Microsoft.Extensions.Hosting;

namespace AxisBus.Repository;

/// <summary>
/// Creates the outbox schema once on startup via the dialect <see cref="IAxisBusStorageInitializer"/> — the
/// bus twin of the cache/saga storage bootstrap. Registered only when
/// <see cref="AxisBusRepositorySettings.RunStartupMigration"/> is set. Runs post-Build, so it honours the
/// host's resolved connection string; idempotent, so a fixture that migrated first makes it a no-op.
/// </summary>
internal sealed class AxisBusStorageInitializerWorker(IAxisBusStorageInitializer initializer) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => initializer.InitializeAsync();
}
