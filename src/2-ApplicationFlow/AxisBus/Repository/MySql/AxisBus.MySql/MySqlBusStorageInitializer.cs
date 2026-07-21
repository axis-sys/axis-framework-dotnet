using AxisBus.MySql.Persistence;
using AxisBus.Repository;
using AxisBus.Repository.Ports;

namespace AxisBus.MySql;

internal sealed class MySqlBusStorageInitializer(AxisBusRepositorySettings settings) : IAxisBusStorageInitializer
{
    public Task InitializeAsync() => AxisBusMigrations.InitializeMySqlAsync(settings.ConnectionString);
}
