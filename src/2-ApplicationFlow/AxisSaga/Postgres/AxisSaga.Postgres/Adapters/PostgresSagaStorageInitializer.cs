using Axis.Ports;
using Axis.Saga;
using AxisSaga.Postgres.Persistence;

namespace AxisSaga.Postgres.Adapters;

/// <inheritdoc/>
internal class PostgresSagaStorageInitializer(AxisSagaSettings settings) : IAxisSagaStorageInitializer
{
    public Task InitializeAsync() => AxisSagaMigrations.InitializePostgresAsync(settings.ConnectionString);
}
