using System.Data.Common;
using Axis.Persistence;
using Npgsql;

namespace AxisSaga.Postgres.Persistence;

internal sealed class AxisSagaPostgresDataSource(NpgsqlDataSource inner, bool ownsInner = true)
    : IAxisSagaConnectionSource, IAsyncDisposable
{
    public NpgsqlDataSource Inner { get; } = inner;

    // Agnostic settings-store seam: NpgsqlDataSource is a DbDataSource, so the portable SAGA_SETTINGS SQL
    // opens its connection through here without any Postgres-specific code.
    public async ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        => await Inner.OpenConnectionAsync(cancellationToken);

    // When the datasource is reused from AxisRepository's keyed registration (ownsInner == false), the
    // repository owns its lifetime — disposing it here would tear down a pool still in use by the BC's
    // unit of work. Only dispose the datasource this wrapper created itself.
    public ValueTask DisposeAsync() => ownsInner ? Inner.DisposeAsync() : ValueTask.CompletedTask;
}
