using Axis.Persistence.Scripts;
using AxisRepository.MySql;

namespace AxisSaga.MySql.Persistence;

public static class AxisSagaMySqlMigrations
{
    public static Task InitializeMySqlAsync(string connectionString)
        => new MySqlMigrationRunner().RunAsync(connectionString, AxisSagaSchema.Schema, AxisSagaSchema.Migrations(new MySqlSqlDialect()));
}
