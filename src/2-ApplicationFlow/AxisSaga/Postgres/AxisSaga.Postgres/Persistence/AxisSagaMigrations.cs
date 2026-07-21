using Axis.Persistence.Scripts;
using AxisRepository.Postgres;

namespace AxisSaga.Postgres.Persistence;

public static class AxisSagaMigrations
{
    public static Task InitializePostgresAsync(string connectionString)
        => new PostgresMigrationRunner().RunAsync(connectionString, AxisSagaSchema.Schema, AxisSagaSchema.Migrations(new PostgresSqlDialect()));
}
