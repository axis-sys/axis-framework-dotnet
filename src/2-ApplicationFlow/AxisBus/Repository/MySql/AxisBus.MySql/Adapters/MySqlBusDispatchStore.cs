using System.Data.Common;
using Axis;
using AxisBus.Repository;
using AxisBus.Repository.Outbox;
using AxisBus.Repository.Persistence;
using AxisBus.Repository.Ports;

namespace AxisBus.MySql.Adapters;

/// <summary>
/// MySQL outbox dispatch store. No shared core (see <see cref="IBusEventDispatchStore"/> remarks) — mirrors
/// <c>MySqlSagaInstanceStore</c>: claim mechanics that would deadlock as a range-scan UPDATE under InnoDB are
/// split into (a) a non-locking discovery of each partition head (a <c>ROW_NUMBER()</c> window), (b) an
/// individual claim UPDATE per head by primary key, and (c) a read-back of the rows actually claimed. Every
/// statement runs through <see cref="MySqlTransientRetry"/> on top of the READ COMMITTED pin applied by
/// <see cref="DependencyInjection"/>. Delivered rows are deleted; failed rows have their lease released.
/// </summary>
internal sealed class MySqlBusDispatchStore(
    IAxisBusConnectionFactory connections,
    IAxisLogger<MySqlBusDispatchStore> logger
) : IBusEventDispatchStore
{
    private const string SelectColumns =
        $"{BusEventsTable.EventId}, {BusEventsTable.OrderingKey}, {BusEventsTable.EnqueueSeq}, {BusEventsTable.EventType}, " +
        $"{BusEventsTable.PayloadJson}, {BusEventsTable.Topics}, {BusEventsTable.TraceId}, {BusEventsTable.JourneyId}, {BusEventsTable.CreatedAt}";

    public async Task<IReadOnlyList<OutboxEvent>> ClaimHeadsAsync(string runner, int leaseSeconds, int batchSize, CancellationToken cancellationToken)
    {
        try
        {
            // (a) Non-locking discovery of each partition's head row (earliest by created/seq/id), due and unclaimed.
            var headIds = await MySqlTransientRetry.ExecuteAsync(async () =>
            {
                await using var conn = await connections.OpenConnectionAsync(cancellationToken);
                await using var cmd = Command(conn,
                    $"""
                     SELECT {BusEventsTable.EventId} FROM (
                         SELECT {BusEventsTable.EventId}, {BusEventsTable.AvailableAt}, {BusEventsTable.ClaimedUntil},
                                ROW_NUMBER() OVER (PARTITION BY {BusEventsTable.OrderingKey}
                                    ORDER BY {BusEventsTable.CreatedAt}, {BusEventsTable.EnqueueSeq}, {BusEventsTable.EventId}) AS rn
                         FROM {BusEventsTable.Table}
                     ) heads
                     WHERE heads.rn = 1
                       AND {BusEventsTable.AvailableAt} <= UTC_TIMESTAMP(6)
                       AND ({BusEventsTable.ClaimedUntil} IS NULL OR {BusEventsTable.ClaimedUntil} < UTC_TIMESTAMP(6))
                     LIMIT @batch
                     """,
                    ("batch", batchSize));

                List<string> ids = [];
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                    ids.Add(reader.GetString(0));
                return ids;
            });

            if (headIds.Count == 0)
                return [];

            // (b) Claim each head individually by primary key — no range scan, so it cannot gap-lock or
            // cross-wait with other claims/inserts.
            List<string> claimedIds = [];
            foreach (var id in headIds)
            {
                var claimed = await MySqlTransientRetry.ExecuteAsync(async () =>
                {
                    await using var conn = await connections.OpenConnectionAsync(cancellationToken);
                    await using var cmd = Command(conn,
                        $"""
                         UPDATE {BusEventsTable.Table}
                         SET {BusEventsTable.ClaimedBy} = @runner,
                             {BusEventsTable.ClaimedUntil} = UTC_TIMESTAMP(6) + INTERVAL @lease SECOND
                         WHERE {BusEventsTable.EventId} = @id
                           AND ({BusEventsTable.ClaimedUntil} IS NULL OR {BusEventsTable.ClaimedUntil} < UTC_TIMESTAMP(6))
                         """,
                        ("runner", runner),
                        ("lease", leaseSeconds),
                        ("id", id));

                    return await cmd.ExecuteNonQueryAsync(cancellationToken) == 1;
                });

                if (claimed)
                    claimedIds.Add(id);
            }

            if (claimedIds.Count == 0)
                return [];

            // (c) No RETURNING in MySQL: read back the rows we just claimed by id.
            return await MySqlTransientRetry.ExecuteAsync<IReadOnlyList<OutboxEvent>>(async () =>
            {
                await using var conn = await connections.OpenConnectionAsync(cancellationToken);
                var names = new string[claimedIds.Count];
                (string Name, object Value)[] parameters = new (string, object)[claimedIds.Count];
                for (var i = 0; i < claimedIds.Count; i++)
                {
                    names[i] = $"@id{i}";
                    parameters[i] = ($"id{i}", claimedIds[i]);
                }

                await using var cmd = Command(conn,
                    $"SELECT {SelectColumns} FROM {BusEventsTable.Table} WHERE {BusEventsTable.EventId} IN ({string.Join(", ", names)})",
                    parameters);

                List<OutboxEvent> rows = [];
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                    rows.Add(Map(reader));
                return rows;
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(MySqlBusDispatchStore)}.{nameof(ClaimHeadsAsync)} failed");
            return [];
        }
    }

    public async Task<AxisResult> DeleteDispatchedAsync(string eventId, string runner, CancellationToken cancellationToken)
    {
        try
        {
            return await MySqlTransientRetry.ExecuteAsync(async () =>
            {
                await using var conn = await connections.OpenConnectionAsync(cancellationToken);
                await using var cmd = Command(conn,
                    $"DELETE FROM {BusEventsTable.Table} WHERE {BusEventsTable.EventId} = @id AND {BusEventsTable.ClaimedBy} = @runner",
                    ("id", eventId),
                    ("runner", runner));

                await cmd.ExecuteNonQueryAsync(cancellationToken);
                return AxisResult.Ok();
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(MySqlBusDispatchStore)}.{nameof(DeleteDispatchedAsync)} failed");
            return AxisError.InternalServerError(AxisBusErrors.PersistenceFailed);
        }
    }

    public async Task<AxisResult> ReleaseAsync(string eventId, string runner, CancellationToken cancellationToken)
    {
        try
        {
            return await MySqlTransientRetry.ExecuteAsync(async () =>
            {
                await using var conn = await connections.OpenConnectionAsync(cancellationToken);
                await using var cmd = Command(conn,
                    $"""
                     UPDATE {BusEventsTable.Table}
                     SET {BusEventsTable.ClaimedBy} = NULL, {BusEventsTable.ClaimedUntil} = NULL
                     WHERE {BusEventsTable.EventId} = @id AND {BusEventsTable.ClaimedBy} = @runner
                     """,
                    ("id", eventId),
                    ("runner", runner));

                await cmd.ExecuteNonQueryAsync(cancellationToken);
                return AxisResult.Ok();
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(MySqlBusDispatchStore)}.{nameof(ReleaseAsync)} failed");
            return AxisError.InternalServerError(AxisBusErrors.PersistenceFailed);
        }
    }

    private static OutboxEvent Map(DbDataReader r)
    {
        var topics = AxisBusSerializer.Deserialize<string[]>(r.GetString(5));
        return new OutboxEvent(
            EventId: r.GetString(0),
            OrderingKey: r.GetString(1),
            EnqueueSeq: r.GetInt32(2),
            EventType: r.GetString(3),
            PayloadJson: r.GetString(4),
            Topics: topics.IsSuccess ? topics.Value : [],
            TraceId: r.IsDBNull(6) ? null : r.GetString(6),
            JourneyId: r.IsDBNull(7) ? null : r.GetString(7),
            CreatedAt: ReadUtc(r, 8));
    }

    private static DateTimeOffset ReadUtc(DbDataReader r, int ordinal)
        => new(DateTime.SpecifyKind(r.GetDateTime(ordinal), DateTimeKind.Utc));

    private static DbCommand Command(DbConnection conn, string sql, params (string Name, object Value)[] parameters)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            cmd.Parameters.Add(p);
        }
        return cmd;
    }
}
