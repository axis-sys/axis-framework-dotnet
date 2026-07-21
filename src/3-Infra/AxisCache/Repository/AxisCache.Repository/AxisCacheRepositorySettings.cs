namespace AxisCache.Repository;

/// <summary>
/// Configuration for the two-tier repository cache. <see cref="ConnectionString"/> points at the L2 SQL
/// store (the same database the application already provisions); <see cref="L1Ttl"/> bounds how long the
/// in-process L1 accelerator trusts a value before falling back to L2 — the only staleness window across
/// instances. Set <see cref="L1Ttl"/> to <see cref="TimeSpan.Zero"/> to bypass L1 entirely (every read
/// hits L2).
/// </summary>
public sealed class AxisCacheRepositorySettings
{
    public required string ConnectionString { get; init; }

    public TimeSpan L1Ttl { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// When true (default), a hosted worker creates the L2 schema on startup (idempotent). Turn it off in
    /// hosts that must not touch the database at boot — e.g. test hosts that fake the storage ports.
    /// </summary>
    public bool RunStartupMigration { get; init; } = true;

    /// <summary>
    /// When true (default), a hosted worker periodically deletes expired L2 rows so reclamation does not
    /// depend on a key being read again. Turn it off in hosts that must not run background database work, or
    /// that delegate the sweep to a dedicated process — expiry is still honoured passively on every read.
    /// </summary>
    public bool SweepEnabled { get; init; } = true;

    /// <summary>How often the sweep worker deletes expired L2 rows while <see cref="SweepEnabled"/> is set.</summary>
    public TimeSpan SweepInterval { get; init; } = TimeSpan.FromMinutes(5);
}
