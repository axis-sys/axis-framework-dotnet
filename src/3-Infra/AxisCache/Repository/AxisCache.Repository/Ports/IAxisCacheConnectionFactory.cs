using System.Data.Common;

namespace AxisCache.Repository.Ports;

/// <summary>
/// Opens a connection to the L2 cache store — the per-provider seam. The Postgres and MySQL adapters each
/// own a pooled data source (outside the application's request-scoped unit of work; cache reads and writes
/// are autocommit and never enlist in a business transaction) and return an open
/// <see cref="DbConnection"/> the shared store drives with plain ADO.NET.
/// </summary>
public interface IAxisCacheConnectionFactory
{
    ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken);
}
