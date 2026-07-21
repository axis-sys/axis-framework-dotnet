using System.Data.Common;

namespace Axis;

/// <summary>
/// A pre-commit collaborator the unit of work drains into its own connection and transaction, immediately
/// before COMMIT, so queued events land atomically with the business state (commit = both, rollback = neither).
/// The default <see cref="NullAxisRepositoryOutbox"/> is a no-op, so a unit of work with no events commits
/// normally; the AxisOutbox adapter supplies the real drain. Kept dialect-agnostic via
/// <see cref="DbConnection"/>/<see cref="DbTransaction"/> so the repository never references a concrete
/// provider or the bus.
/// </summary>
public interface IAxisRepositoryOutbox
{
    /// <summary>
    /// Writes any queued events into <paramref name="connection"/>/<paramref name="transaction"/> — the unit
    /// of work's live, not-yet-committed transaction. Returns a failure to abort the commit (the caller then
    /// rolls back, so neither the state nor the events persist). A no-op returns <c>Ok</c>.
    /// </summary>
    Task<AxisResult> DrainAsync(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken);
}
