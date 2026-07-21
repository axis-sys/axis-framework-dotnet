using Axis;
using AxisBus.Repository.Outbox;

namespace AxisBus.Repository.Ports;

/// <summary>
/// The dispatch-side outbox store: claim the head of each partition, delete a dispatched row, release a lease
/// on failure. Claim mechanics genuinely diverge between databases (Postgres <c>DISTINCT ON</c> vs MySQL
/// window function; and MySQL cannot range-scan-claim safely under InnoDB — the same deadlock class
/// <c>MySqlSagaInstanceStore</c> hit), so — mirroring <c>ISagaInstanceStore</c> — this port has NO shared core
/// implementation; <c>AxisBus.Postgres</c> and <c>AxisBus.MySql</c> each implement it fully.
/// </summary>
/// <remarks>
/// FIFO per partition: only the HEAD row of a partition (lowest (CREATED_AT, ENQUEUE_SEQ, EVENT_ID) for an
/// ORDERING_KEY) is ever claimed, so the next row in a partition is delivered only after the current head is
/// deleted. Ownership on every mutation is guarded by <c>CLAIMED_BY = @runner</c> alone (no VERSION column) —
/// a lost lease makes the mutation a no-op over zero rows, not an error. There is no terminal status and no
/// retention: a delivered row is deleted; a failed row keeps its place (lease released) and is re-claimed on
/// the next pass. The worker, not the row, owns the backoff.
/// </remarks>
public interface IBusEventDispatchStore
{
    /// <summary>
    /// Claims the head row of up to <paramref name="batchSize"/> distinct due partitions (AVAILABLE_AT ≤ now,
    /// lease absent or expired) for <paramref name="runner"/>, setting the lease, and returns the claimed rows.
    /// </summary>
    Task<IReadOnlyList<OutboxEvent>> ClaimHeadsAsync(string runner, int leaseSeconds, int batchSize, CancellationToken cancellationToken);

    /// <summary>Deletes a successfully dispatched row. No-op (not an error) if <paramref name="runner"/> no longer owns the lease.</summary>
    Task<AxisResult> DeleteDispatchedAsync(string eventId, string runner, CancellationToken cancellationToken);

    /// <summary>
    /// Releases the lease on a row whose delivery failed, leaving it in place so it is re-claimable on the next
    /// pass (ordering preserved — the partition head does not advance). No-op if the lease is no longer owned.
    /// </summary>
    Task<AxisResult> ReleaseAsync(string eventId, string runner, CancellationToken cancellationToken);
}
