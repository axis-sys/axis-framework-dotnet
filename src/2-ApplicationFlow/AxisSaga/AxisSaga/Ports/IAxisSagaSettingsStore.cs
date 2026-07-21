namespace Axis.Ports;

/// <summary>
/// Consumer-facing management API for the saga runtime settings persisted in <c>AXIS_SAGA.SAGA_SETTINGS</c>
/// — today the single global concurrency cap (<c>MAX_CONCURRENT_SAGAS</c>). It lets an application read and
/// adjust the cap at runtime (e.g. right after a deployment or a schema migration) without a redeploy and
/// without hand-rolled SQL. A single dialect-agnostic implementation over ADO.NET serves every storage
/// (Postgres, MySQL, …): the settings SQL is portable, so there is one source of truth. Every method
/// converts exceptions into <see cref="AxisResult"/> and never throws.
/// </summary>
public interface IAxisSagaSettingsStore
{
    /// <summary>
    /// Reads the global concurrency cap (<c>MAX_CONCURRENT_SAGAS</c>). <c>Ok(null)</c> means unbounded (no
    /// cap); <c>Ok(n)</c> is the current cap on how many sagas may hold a live lease at once.
    /// </summary>
    Task<AxisResult<int?>> GetMaxConcurrentSagasAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the global concurrency cap unconditionally. <paramref name="maxConcurrentSagas"/> == <c>null</c>
    /// makes the runtime unbounded; a positive value caps the live leases. Zero or negative is rejected with
    /// a validation error (a cap of 0 would stall every saga). This overwrites the current value — use
    /// <see cref="TrySetMaxConcurrentSagasAsync"/> to preserve a value that may have been tuned by hand.
    /// </summary>
    Task<AxisResult> SetMaxConcurrentSagasAsync(int? maxConcurrentSagas, CancellationToken cancellationToken = default);

    /// <summary>
    /// Race-safe conditional update: writes <paramref name="newValue"/> only if the stored cap still equals
    /// <paramref name="expectedCurrent"/>. Returns <c>Ok(true)</c> when it changed the row, <c>Ok(false)</c>
    /// when the guard did not match (someone already changed it, or it was never <paramref name="expectedCurrent"/>).
    /// This is the canonical "raise the seeded default to X, but only while it still holds the seed" operation,
    /// done as one atomic statement instead of a racy read-then-set — so two concurrent callers can't both
    /// raise it and a value someone tuned by hand is never clobbered. <paramref name="expectedCurrent"/> is a
    /// concrete value by design (a portable <c>=</c> guard: a stored <c>NULL</c>/unbounded cap never matches);
    /// <paramref name="newValue"/> == <c>null</c> means unbounded, and zero or negative is rejected with a
    /// validation error.
    /// </summary>
    Task<AxisResult<bool>> TrySetMaxConcurrentSagasAsync(int expectedCurrent, int? newValue, CancellationToken cancellationToken = default);
}
