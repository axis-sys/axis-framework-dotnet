using Axis.Ddl;

namespace Axis.Persistence.Scripts;

internal static class SagaDefinitionsTable
{
    public const string Table = $"{AxisSagaSchema.Schema}.SAGA_DEFINITIONS";

    public const string SagaName = "SAGA_NAME";
    public const string DefinitionHash = "DEFINITION_HASH";
    public const string DefinitionJson = "DEFINITION_JSON";
    public const string UpdatedAt = "UPDATED_AT";

    public static AxisTable Define() => new AxisTable(Table)
        .Column(SagaName, AxisDbType.Varchar(100), primaryKey: true)
        .Column(DefinitionHash, AxisDbType.Varchar(64), notNull: true)
        .Column(DefinitionJson, AxisDbType.Json, notNull: true)
        .Column(UpdatedAt, AxisDbType.TimestampUtc, notNull: true, @default: AxisDefault.NowUtc);
}
