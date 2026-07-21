using Axis.Ports;
using Axis.Saga;
using AxisSaga.MySql.Persistence;

namespace AxisSaga.MySql.Adapters;

/// <inheritdoc/>
internal class MySqlSagaStorageInitializer(AxisSagaSettings settings) : IAxisSagaStorageInitializer
{
    public Task InitializeAsync() => AxisSagaMySqlMigrations.InitializeMySqlAsync(settings.ConnectionString);
}
