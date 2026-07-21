using System.Data.Common;
using AxisCache.Repository.Ports;
using MySqlConnector;

namespace AxisCache.MySql;

/// <summary>Opens L2 connections from a pooled <see cref="MySqlDataSource"/> the adapter owns for the process.</summary>
internal sealed class MySqlCacheConnectionFactory(MySqlDataSource dataSource) : IAxisCacheConnectionFactory, IAsyncDisposable
{
    public async ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        => await dataSource.OpenConnectionAsync(cancellationToken);

    public ValueTask DisposeAsync() => dataSource.DisposeAsync();
}
