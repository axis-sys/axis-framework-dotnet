using System.Data.Common;

namespace Axis;

/// <summary>
/// No-op outbox registered by default (via <c>TryAdd</c>) so a unit of work with no events — or a host that
/// never installed AxisOutbox — commits normally. Installing AxisOutbox replaces this with the real drain.
/// </summary>
public sealed class NullAxisRepositoryOutbox : IAxisRepositoryOutbox
{
    public Task<AxisResult> DrainAsync(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken)
        => Task.FromResult(AxisResult.Ok());
}
