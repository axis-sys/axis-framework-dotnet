using System.Data.Common;
using Axis.Persistence;
using MySqlConnector;

namespace AxisSaga.MySql.Persistence;

internal sealed class AxisSagaMySqlDataSource(MySqlDataSource inner, bool ownsInner = true) : IAxisSagaConnectionSource, IAsyncDisposable
{
    public MySqlDataSource Inner { get; } = inner;

    public async ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        => await Inner.OpenConnectionAsync(cancellationToken);

    public ValueTask DisposeAsync() => ownsInner ? Inner.DisposeAsync() : ValueTask.CompletedTask;
}
