using Axis.Ddl;

namespace Axis.Persistence.Scripts;

internal static class SagaInstancesTable
{
    public const string Table = $"{AxisSagaSchema.Schema}.SAGA_INSTANCES";

    public const string SagaId = "SAGA_ID";
    public const string SagaName = "SAGA_NAME";
    public const string Status = "STATUS";
    public const string CurrentStage = "CURRENT_STAGE";
    public const string PayloadJson = "PAYLOAD_JSON";
    public const string LastErrorCode = "LAST_ERROR_CODE";
    public const string LastErrorMessage = "LAST_ERROR_MESSAGE";
    public const string Version = "VERSION";
    public const string CreatedAt = "CREATED_AT";
    public const string UpdatedAt = "UPDATED_AT";
    public const string RetainForSeconds = "RETAIN_FOR_SECONDS";
    public const string DeleteNotBefore = "DELETE_NOT_BEFORE";
    public const string ClaimedBy = "CLAIMED_BY";
    public const string ClaimedUntil = "CLAIMED_UNTIL";

    // RETAIN_FOR_SECONDS / DELETE_NOT_BEFORE: retention window for deleting a terminal saga later.
    // CLAIMED_BY / CLAIMED_UNTIL: the execution LEASE (heartbeat-refreshed) — replaces a held advisory lock.
    // The two partial indexes (DELETE_NOT_BEFORE not-null; active-lease) keep the janitor and the global
    // concurrency-cap live-lease COUNT selective on Postgres; MySQL renders them as plain indexes.
    public static AxisTable Define() => new AxisTable(Table)
        .Column(SagaId, AxisDbType.Varchar(50), primaryKey: true)
        .Column(SagaName, AxisDbType.Varchar(100), notNull: true)
        .Column(Status, AxisDbType.Varchar(30), notNull: true)
        .Column(CurrentStage, AxisDbType.Varchar(50))
        .Column(PayloadJson, AxisDbType.Json, notNull: true)
        .Column(LastErrorCode, AxisDbType.Varchar(100))
        .Column(LastErrorMessage, AxisDbType.Text)
        .Column(Version, AxisDbType.Int, notNull: true, @default: AxisDefault.Int(1))
        .Column(CreatedAt, AxisDbType.TimestampUtc, notNull: true, @default: AxisDefault.NowUtc)
        .Column(UpdatedAt, AxisDbType.TimestampUtc, notNull: true, @default: AxisDefault.NowUtc)
        .Column(RetainForSeconds, AxisDbType.Int)
        .Column(DeleteNotBefore, AxisDbType.TimestampUtc)
        .Column(ClaimedBy, AxisDbType.Varchar(50))
        .Column(ClaimedUntil, AxisDbType.TimestampUtc)
        .Index("IDX_SAGA_INSTANCES_STATUS_UPDATED", Status, UpdatedAt)
        .Index("IDX_SAGA_INSTANCES_NAME", SagaName)
        .PartialIndex("IDX_SAGA_INSTANCES_DELETE_NOT_BEFORE", $"{DeleteNotBefore} IS NOT NULL", DeleteNotBefore)
        .Index("IDX_SAGA_INSTANCES_LEASE", Status, ClaimedUntil)
        .PartialIndex("IDX_SAGA_INSTANCES_ACTIVE_LEASE", $"{Status} NOT IN ('Completed','Failed','Compensated')", ClaimedUntil);
}
