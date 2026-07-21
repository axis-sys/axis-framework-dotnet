using System.Data.Common;
using AxisBus.Repository.Ports;
using MySqlConnector;

namespace AxisBus.MySql;

/// <summary>
/// Opens outbox connections from a pooled <see cref="MySqlDataSource"/> the adapter owns for the process —
/// shared by the dialect-agnostic publish-path store and by <c>Adapters.MySqlBusDispatchStore</c>. The data
/// source is pinned to READ COMMITTED on every new physical connection (see <see cref="DependencyInjection"/>).
/// </summary>
internal sealed class MySqlBusConnectionFactory(MySqlDataSource dataSource) : IAxisBusConnectionFactory, IAsyncDisposable
{
    public async ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        => await dataSource.OpenConnectionAsync(cancellationToken);

    public ValueTask DisposeAsync() => dataSource.DisposeAsync();
}
