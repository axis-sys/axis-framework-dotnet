namespace Axis.Saga;

/// <summary>
/// Dialect-agnostic saga runtime settings, shared by every storage adapter (Postgres, MySQL, …). The
/// <see cref="ConnectionString"/> is the only field the adapter interprets; the rest tune the
/// engine/resumer behaviour and are dialect-neutral.
/// </summary>
public class AxisSagaSettings
{
    public required string ConnectionString { get; init; }
    public TimeSpan ResumerPollInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Doubles as the saga execution LEASE duration: a run claims the instance for this long and a
    /// heartbeat renews it every <c>ResumeAfter / 4</c> while stages execute. A saga whose lease is not
    /// renewed for this long (owner crashed/hung, or dispatch dropped) is considered stale and re-fired
    /// by the resumer. Set it comfortably above the worst-case single-stage duration so a
    /// legitimately-running stage is never reclaimed mid-flight.
    /// </summary>
    public TimeSpan ResumeAfter { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Maximum number of stale sagas a single resumer poll claims at once (the <c>LIMIT</c> of the
    /// claim query). Keeps each poll bounded and lets several nodes share the backlog.
    /// </summary>
    public int ResumeBatchSize { get; init; } = 100;

    /// <summary>
    /// When <c>true</c> (the default) the storage adapter hosts the built-in resumer worker: on startup
    /// it runs the saga schema migration and initializes the registered definitions, then polls the
    /// resumer every <see cref="ResumerPollInterval"/>. Set it to <c>false</c> on processes that
    /// start/await sagas but must not run the background loop, or tests with no live database.
    /// </summary>
    public bool ResumerEnabled { get; init; } = true;

    /// <summary>
    /// Total number of times a stage handler is invoked before its transient failure (deadlock,
    /// serialization, connection blip — any <see cref="AxisError.IsTransient"/> error) is treated as a
    /// real failure that routes to compensation. <c>1</c> disables retry (a single attempt). Dialect-neutral:
    /// the engine reacts to the typed transient classification (<c>AxisError.IsTransient</c>), not to any
    /// provider. A stage may override this via <c>RetryOnTransient</c> in the saga definition.
    /// </summary>
    public int TransientRetryMaxAttempts { get; init; } = 3;

    /// <summary>
    /// Base backoff between transient retry attempts; the engine scales it by the attempt number and adds
    /// a small jitter so racing runners desynchronize. Overridable per stage via <c>RetryOnTransient</c>.
    /// </summary>
    public TimeSpan TransientRetryBaseDelay { get; init; } = TimeSpan.FromMilliseconds(100);
}
