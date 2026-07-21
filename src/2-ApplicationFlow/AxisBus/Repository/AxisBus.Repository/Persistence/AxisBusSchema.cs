using Axis.Ddl;

namespace AxisBus.Repository.Persistence;

/// <summary>
/// The AxisBus outbox schema, declared ONCE and dialect-agnostic — mirrors the AxisCache/AxisSaga schema
/// pattern. Each adapter renders it with its own <see cref="IAxisSqlDialect"/> and applies it with the
/// framework <see cref="IAxisMigrationRunner"/>. In MySQL a SCHEMA is a database; the runner issues
/// <c>CREATE SCHEMA IF NOT EXISTS</c> for it.
/// </summary>
public static class AxisBusSchema
{
    public const string Schema = BusEventsTable.Schema;

    /// <summary>The migration set for the given dialect — a single consolidated V1 that renders the table.</summary>
    public static (string Version, string Script)[] Migrations(IAxisSqlDialect dialect) =>
    [
        ("V1", BusEventsTable.Define().Render(dialect)),
    ];
}
