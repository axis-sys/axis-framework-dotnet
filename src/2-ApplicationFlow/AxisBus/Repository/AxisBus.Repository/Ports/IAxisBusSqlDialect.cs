namespace AxisBus.Repository.Ports;

/// <summary>
/// The outbox SQL that genuinely diverges between databases — for now only the INSERT used by the drain path
/// (<c>RepositoryOutboxDrain</c>): Postgres needs an explicit <c>::JSONB</c> cast for the JSON columns, MySQL
/// does not (mirrors <c>IAxisCacheSqlDialect.UpsertSql</c>). Parameters, in order: <c>@eventId</c>,
/// <c>@orderingKey</c>, <c>@enqueueSeq</c>, <c>@eventType</c>, <c>@payloadJson</c>, <c>@topics</c>,
/// <c>@traceId</c>, <c>@journeyId</c>, <c>@createdAt</c>, <c>@availableAt</c>.
/// </summary>
/// <remarks>
/// This interface stays intentionally minimal — it carries only the INSERT. The dispatcher's claim (head of
/// each partition), delete-after-dispatch and lease-release SQL diverges structurally between databases, so it
/// lives in the separate <see cref="IBusEventDispatchStore"/> port — implemented in full per dialect.
/// </remarks>
public interface IAxisBusSqlDialect
{
    string InsertSql { get; }
}
