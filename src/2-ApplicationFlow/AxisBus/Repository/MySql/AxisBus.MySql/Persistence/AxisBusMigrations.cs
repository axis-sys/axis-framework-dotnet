using AxisBus.Repository.Persistence;
using AxisRepository.MySql;

namespace AxisBus.MySql.Persistence;

public static class AxisBusMigrations
{
    // The same AxisBusSchema (core) rendered with the MySQL dialect and applied by the runner.
    // Idempotent — safe to run at every startup.
    public static Task InitializeMySqlAsync(string connectionString)
        => new MySqlMigrationRunner().RunAsync(connectionString, AxisBusSchema.Schema, AxisBusSchema.Migrations(new MySqlSqlDialect()));
}
