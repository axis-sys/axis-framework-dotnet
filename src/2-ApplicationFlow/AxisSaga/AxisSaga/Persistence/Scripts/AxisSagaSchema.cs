using Axis.Ddl;

namespace Axis.Persistence.Scripts;

/// <summary>
/// The AxisSaga store schema, declared ONCE and dialect-agnostic. Each table is an <see cref="AxisTable"/>;
/// an injected <see cref="IAxisSqlDialect"/> (Postgres or MySQL) renders the concrete DDL — so there is a
/// single source of truth for column names and structure, and the two adapters differ only by which dialect
/// they pass. In MySQL a SCHEMA is a database; the runner issues <c>CREATE SCHEMA IF NOT EXISTS</c> for it.
/// </summary>
public static class AxisSagaSchema
{
    public const string Schema = "AXIS_SAGA";

    // Order matters: SAGA_INSTANCES first (SAGA_STAGE_LOGS foreign-keys it); the other two are independent.
    public static IReadOnlyList<AxisTable> Tables =>
    [
        SagaInstancesTable.Define(),
        SagaStageLogsTable.Define(),
        SagaDefinitionsTable.Define(),
        SagaSettingsTable.Define(),
    ];

    /// <summary>The migration set for the given dialect — a single consolidated V1 that renders every table.</summary>
    public static (string Version, string Script)[] Migrations(IAxisSqlDialect dialect) =>
    [
        ("V1", string.Join("\n", Tables.Select(table => table.Render(dialect)))),
    ];
}
