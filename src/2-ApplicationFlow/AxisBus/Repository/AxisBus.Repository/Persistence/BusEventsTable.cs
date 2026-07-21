using Axis.Ddl;

namespace AxisBus.Repository.Persistence;

/// <summary>
/// The single outbox table, declared ONCE and dialect-agnostic. An injected <see cref="IAxisSqlDialect"/>
/// (Postgres or MySQL) renders the concrete DDL, so column names live in one place and the two adapters
/// differ only by dialect.
/// </summary>
/// <remarks>
/// This is the atomic transactional outbox: rows are written inside the business unit of work's own
/// connection/transaction (see <c>RepositoryOutboxDrain</c>), and deleted the moment they are dispatched
/// (see <see cref="Ports.IBusEventDispatchStore"/>) — there is no terminal <c>Processed</c> status and no
/// retention window; the presence of a row IS the "pending" state, and delivery is its deletion. Ordering is
/// per <see cref="OrderingKey"/> (a logical partition), FIFO within a key by
/// (<see cref="CreatedAt"/>, <see cref="EnqueueSeq"/>); distinct keys dispatch in parallel.
/// <see cref="ClaimedBy"/>/<see cref="ClaimedUntil"/> is the execution LEASE (same pattern as
/// <c>SAGA_INSTANCES</c>) so multiple instances never process the same partition head twice.
/// </remarks>
internal static class BusEventsTable
{
    public const string Schema = "AXIS_OUTBOX";

    public const string Table = $"{Schema}.OUTBOX_EVENTS";

    public const string EventId = "EVENT_ID";
    public const string OrderingKey = "ORDERING_KEY";
    public const string EnqueueSeq = "ENQUEUE_SEQ";
    public const string EventType = "EVENT_TYPE";
    public const string PayloadJson = "PAYLOAD_JSON";
    public const string Topics = "TOPICS";
    public const string TraceId = "TRACE_ID";
    public const string JourneyId = "JOURNEY_ID";
    public const string CreatedAt = "CREATED_AT";
    public const string AvailableAt = "AVAILABLE_AT";
    public const string ClaimedBy = "CLAIMED_BY";
    public const string ClaimedUntil = "CLAIMED_UNTIL";

    public static AxisTable Define() => new AxisTable(Table)
        .Column(EventId, AxisDbType.Varchar(50), primaryKey: true)
        .Column(OrderingKey, AxisDbType.Varchar(200), notNull: true)
        .Column(EnqueueSeq, AxisDbType.Int, notNull: true)
        .Column(EventType, AxisDbType.Varchar(1000), notNull: true)
        .Column(PayloadJson, AxisDbType.Json, notNull: true)
        // Parenthesized on purpose: MySQL 8.0.13+ rejects a bare literal DEFAULT on JSON/TEXT/BLOB columns and
        // only accepts an expression default (wrapped in parens); Postgres accepts the parens too, so one Raw
        // string is portable across both dialects.
        .Column(Topics, AxisDbType.Json, notNull: true, @default: AxisDefault.Raw("('[]')"))
        .Column(TraceId, AxisDbType.Varchar(100))
        .Column(JourneyId, AxisDbType.Varchar(100))
        .Column(CreatedAt, AxisDbType.TimestampUtc, notNull: true, @default: AxisDefault.NowUtc)
        .Column(AvailableAt, AxisDbType.TimestampUtc, notNull: true, @default: AxisDefault.NowUtc)
        .Column(ClaimedBy, AxisDbType.Varchar(100))
        .Column(ClaimedUntil, AxisDbType.TimestampUtc)
        // Covers the claim query: order within a partition by creation time then the local enqueue sequence
        // (the tie-breaker for events co-created in the same instant), with EVENT_ID as the final total-order key.
        .Index("IDX_OUTBOX_DISPATCH", OrderingKey, CreatedAt, EnqueueSeq);
}
