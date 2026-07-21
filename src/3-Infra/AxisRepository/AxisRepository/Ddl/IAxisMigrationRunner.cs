namespace Axis.Ddl;

/// <summary>
/// Applies a Bounded Context's pending migrations to one schema, idempotently — the swappable infra port for
/// schema migration, the "apply" half that pairs with <see cref="IAxisSqlDialect"/>'s "render" half. Each
/// database adapter (AxisRepository.Postgres / AxisRepository.MySql) provides an implementation owning its own
/// bootstrap, concurrency lock, and transaction semantics (Postgres: transactional advisory lock; MySQL:
/// session named lock with per-version commit). A dialect-agnostic caller migrates any provider by swapping the
/// injected runner together with the matching dialect.
/// </summary>
public interface IAxisMigrationRunner
{
    /// <summary>
    /// Bootstraps <paramref name="schema"/> and its <c>MIGRATIONS</c> control table, then applies each pending
    /// version in <paramref name="migrations"/> in order, skipping any already recorded.
    /// </summary>
    Task RunAsync(string connectionString, string schema, (string Version, string Script)[] migrations);
}
