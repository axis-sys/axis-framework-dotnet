using System.Data.Common;
using AxisBus.Repository.Ports;
using Npgsql;

namespace AxisBus.Postgres;

/// <summary>Opens outbox connections from a pooled <see cref="NpgsqlDataSource"/> the adapter owns for the process.</summary>
internal sealed class PostgresBusConnectionFactory(NpgsqlDataSource dataSource) : IAxisBusConnectionFactory, IAsyncDisposable
{
    public async ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        => await dataSource.OpenConnectionAsync(cancellationToken);

    public ValueTask DisposeAsync() => dataSource.DisposeAsync();
}
