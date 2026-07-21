using AxisBus.Postgres.Persistence;
using AxisBus.Repository;
using AxisBus.Repository.Ports;

namespace AxisBus.Postgres;

internal sealed class PostgresBusStorageInitializer(AxisBusRepositorySettings settings) : IAxisBusStorageInitializer
{
    public Task InitializeAsync() => AxisBusMigrations.InitializePostgresAsync(settings.ConnectionString);
}
