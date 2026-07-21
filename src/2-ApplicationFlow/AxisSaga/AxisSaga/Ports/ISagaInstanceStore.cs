using Axis.SharedKernel;

namespace Axis.Ports;

/// <summary>
/// Persistence boundary for the saga instance row (the <c>SAGA_INSTANCES</c> table) plus the global
/// concurrency settings. This is the dialect seam: the engine, mediator, resumer and janitor never
/// spell out SQL — they call this port, and each database (Postgres, MySQL, …) provides one
/// implementation. Every mutation uses optimistic concurrency on the version AND lease ownership
/// (<c>CLAIMED_BY</c> + <c>CLAIMED_UNTIL</c>) so a run that lost its lease can never mutate the saga;
/// every method converts exceptions into <see cref="AxisResult"/> and never throws.
/// </summary>
public interface ISagaInstanceStore
{
    /// <summary>
    /// Inserts a brand-new instance in <c>Pending</c> with version 1. A duplicate id must be mapped to
    /// <c>AxisError.Conflict("SAGA_ID_ALREADY_EXISTS")</c> by the dialect implementation.
    /// </summary>
    Task<AxisResult> InsertAsync(string sagaId, string sagaName, string payloadJson, int? retainForSeconds);

    /// <summary>
    /// Atomically claims the saga for one engine run by stamping <c>CLAIMED_BY</c>/<c>CLAIMED_UNTIL</c>,
    /// only if it is non-terminal, its lease is absent or expired, and the global concurrency cap (when
    /// set) is not yet reached. Returns the freshly-loaded instance on success, or <c>null</c> when the
    /// saga is gone, terminal, held by another live run, or the cap is full (the caller skips).
    /// </summary>
    Task<AxisSagaInstance?> AcquireLeaseAsync(string sagaId, string runner, int leaseSeconds);

    /// <summary>
    /// Heartbeat: extends this run's lease if it still owns it and the saga is non-terminal. Returns
    /// <c>false</c> when the lease was lost or the saga reached a terminal state — the caller must then
    /// stop driving the saga.
    /// </summary>
    Task<bool> ExtendLeaseAsync(string sagaId, string runner, int leaseSeconds);

    Task<AxisResult<AxisSagaInstance>> LoadAsync(string sagaId);

    /// <summary>Returns the raw persisted payload JSON, or <c>null</c> if the row is gone. The caller deserializes.</summary>
    Task<string?> ReloadPayloadJsonAsync(string sagaId);

    Task<AxisResult> MoveToStatusAsync(
        string sagaId, int expectedVersion, string runner, AxisSagaStatus newStatus, string? currentStage,
        string? errorCode = null, string? errorMessage = null);

    Task<AxisResult> PersistStageSuccessAsync(
        string sagaId, int expectedVersion, string runner, string stageName, string payloadJson,
        bool keepCurrentStage = false);

    Task<AxisResult> CompleteAsync(string sagaId, int expectedVersion, string runner);

    Task<AxisResult> CompensateAsync(string sagaId, int expectedVersion, string runner);

    Task<AxisResult> FailAsync(string sagaId, int expectedVersion, string runner, string errorCode, string? errorMessage = null);

    /// <summary>
    /// Selects up to <paramref name="limit"/> non-terminal sagas whose lease has expired — the staleness
    /// signal the resumer re-fires. Ordering and any row-skip semantics are the dialect's concern.
    /// </summary>
    Task<IReadOnlyList<string>> ClaimStaleSagaIdsAsync(int limit, CancellationToken cancellationToken);

    /// <summary>Counts sagas currently holding a live lease (the set the global cap gates on).</summary>
    Task<int> CountLiveLeasesAsync(CancellationToken cancellationToken);

    /// <summary>Reads the global concurrency cap (<c>MAX_CONCURRENT_SAGAS</c>); <c>null</c> = unbounded.</summary>
    Task<int?> GetMaxConcurrentSagasAsync(CancellationToken cancellationToken);

    /// <summary>Deletes up to <paramref name="batchSize"/> terminal sagas whose retention window has elapsed.</summary>
    Task<int> DeleteExpiredAsync(int batchSize, CancellationToken cancellationToken);
}
