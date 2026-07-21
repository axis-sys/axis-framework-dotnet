using Axis.Ddl;

namespace Axis.Persistence.Scripts;

internal static class SagaStageLogsTable
{
    public const string Table = $"{AxisSagaSchema.Schema}.SAGA_STAGE_LOGS";

    public const string LogId = "LOG_ID";
    public const string SagaId = "SAGA_ID";
    public const string StageName = "STAGE_NAME";
    public const string Attempt = "ATTEMPT";
    public const string Status = "STATUS";
    public const string ErrorCode = "ERROR_CODE";
    public const string ErrorMessage = "ERROR_MESSAGE";
    public const string StartedAt = "STARTED_AT";
    public const string FinishedAt = "FINISHED_AT";

    public static AxisTable Define() => new AxisTable(Table)
        .Column(LogId, AxisDbType.Varchar(50), primaryKey: true)
        .Column(SagaId, AxisDbType.Varchar(50), notNull: true)
        .Column(StageName, AxisDbType.Varchar(50), notNull: true)
        .Column(Attempt, AxisDbType.Int, notNull: true, @default: AxisDefault.Int(1))
        .Column(Status, AxisDbType.Varchar(30), notNull: true)
        .Column(ErrorCode, AxisDbType.Varchar(100))
        .Column(ErrorMessage, AxisDbType.Text)
        .Column(StartedAt, AxisDbType.TimestampUtc, notNull: true, @default: AxisDefault.NowUtc)
        .Column(FinishedAt, AxisDbType.TimestampUtc)
        .Index("IDX_SAGA_STAGE_LOGS_SAGA_STAGE_STATUS", SagaId, StageName, Status)
        .ForeignKey("FK_SAGA_STAGE_LOGS_SAGA", SagaId, SagaInstancesTable.Table, SagaInstancesTable.SagaId, onDeleteCascade: true);
}
