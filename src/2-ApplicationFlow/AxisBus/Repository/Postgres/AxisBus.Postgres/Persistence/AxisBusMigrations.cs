using AxisBus.Repository.Persistence;
using AxisRepository.Postgres;

namespace AxisBus.Postgres.Persistence;

public static class AxisBusMigrations
{
    // The AXIS_BUS schema is declared ONCE in the core (AxisBusSchema); here it is rendered with the
    // Postgres dialect and applied by the framework runner. Idempotent — safe to run at every startup.
    public static Task InitializePostgresAsync(string connectionString)
        => new PostgresMigrationRunner().RunAsync(connectionString, AxisBusSchema.Schema, AxisBusSchema.Migrations(new PostgresSqlDialect()));
}
