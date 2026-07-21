using AxisCache.Repository.Persistence;
using AxisRepository.MySql;

namespace AxisCache.MySql.Persistence;

public static class AxisCacheMigrations
{
    // The same AxisCacheSchema (core) rendered with the MySQL dialect and applied by the runner.
    // Idempotent — safe to run at every startup.
    public static Task InitializeMySqlAsync(string connectionString)
        => new MySqlMigrationRunner().RunAsync(connectionString, AxisCacheSchema.Schema, AxisCacheSchema.Migrations(new MySqlSqlDialect()));
}
