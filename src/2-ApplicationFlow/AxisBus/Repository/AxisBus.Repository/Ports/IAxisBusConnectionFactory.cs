using System.Data.Common;

namespace AxisBus.Repository.Ports;

/// <summary>
/// Opens a connection to the outbox store — the per-provider seam. The Postgres and MySQL adapters each own
/// a pooled data source (outside the application's request-scoped unit of work; outbox writes are autocommit
/// and never enlist in a business transaction) and return an open <see cref="DbConnection"/> the shared store
/// drives with plain ADO.NET.
/// </summary>
public interface IAxisBusConnectionFactory
{
    ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken);
}
