using AxisCache.Repository.Persistence;
using AxisRepository.Postgres;

namespace AxisCache.Postgres.Persistence;

public static class AxisCacheMigrations
{
    // The AXIS_CACHE schema is declared ONCE in the core (AxisCacheSchema); here it is rendered with the
    // Postgres dialect and applied by the framework runner. Idempotent — safe to run at every startup.
    public static Task InitializePostgresAsync(string connectionString)
        => new PostgresMigrationRunner().RunAsync(connectionString, AxisCacheSchema.Schema, AxisCacheSchema.Migrations(new PostgresSqlDialect()));
}
