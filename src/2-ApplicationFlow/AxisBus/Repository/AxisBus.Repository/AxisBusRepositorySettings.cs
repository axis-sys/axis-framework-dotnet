namespace AxisBus.Repository;

/// <summary>
/// Configuration for the atomic outbox bus. <see cref="ConnectionString"/> points at the SQL store the
/// dispatcher reads (the same database the application writes its events into, atomically, via the unit of
/// work). The remaining settings tune the dispatcher, which drains the outbox with a poll/lease loop
/// (<see cref="BusDispatcher"/> driven by the <see cref="AxisBusDispatcherWorker"/> poll worker).
/// </summary>
public sealed class AxisBusRepositorySettings
{
    public required string ConnectionString { get; init; }

    /// <summary>How often the dispatcher poll worker runs a pass, and the floor of the backoff. Consumed by <see cref="AxisBusDispatcherWorker"/>.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Ceiling of the worker's exponential backoff when deliveries keep failing. Consumed by <see cref="AxisBusDispatcherWorker"/>.</summary>
    public TimeSpan MaxPollBackoff { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>How long a claimed partition head's lease is held before another instance may reclaim it. Passed to the claim as the lease window.</summary>
    public TimeSpan LeaseDuration { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Max distinct partitions claimed per dispatcher poll (one head row each). Consumed by <see cref="BusDispatcher"/>.</summary>
    public int BatchSize { get; init; } = 50;

    /// <summary>Whether the dispatcher poll worker is registered in this host — set false on hosts that only publish and never drain the outbox.</summary>
    public bool DispatcherEnabled { get; init; } = true;

    /// <summary>
    /// When true (default), a hosted worker creates the outbox schema on startup (idempotent). Turn it off in
    /// hosts that must not touch the database at boot — e.g. test hosts that fake the storage ports.
    /// </summary>
    public bool RunStartupMigration { get; init; } = true;
}
