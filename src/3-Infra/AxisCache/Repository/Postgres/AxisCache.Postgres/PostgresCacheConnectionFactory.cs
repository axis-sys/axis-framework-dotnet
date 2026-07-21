using System.Data.Common;
using AxisCache.Repository.Ports;
using Npgsql;

namespace AxisCache.Postgres;

/// <summary>Opens L2 connections from a pooled <see cref="NpgsqlDataSource"/> the adapter owns for the process.</summary>
internal sealed class PostgresCacheConnectionFactory(NpgsqlDataSource dataSource) : IAxisCacheConnectionFactory, IAsyncDisposable
{
    public async ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        => await dataSource.OpenConnectionAsync(cancellationToken);

    public ValueTask DisposeAsync() => dataSource.DisposeAsync();
}
