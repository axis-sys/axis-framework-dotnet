using System.Data.Common;
using Axis;
using AxisBus.Repository;
using AxisBus.Repository.Outbox;
using AxisBus.Repository.Persistence;
using AxisBus.Repository.Ports;

namespace AxisBus.Postgres.Adapters;

/// <summary>
/// Postgres outbox dispatch store. Claims the HEAD of each due partition — a non-locking <c>DISTINCT ON</c>
/// discovery (earliest row per <c>ORDERING_KEY</c>), then an individual claim UPDATE per head by primary key
/// (with the not-claimed guard) so two dispatchers never take the same partition head — then reads the claimed
/// rows back. Delivered rows are deleted; failed rows have their lease released and stay in place (FIFO
/// preserved). Every mutation re-checks <c>CLAIMED_BY = @runner</c>, so a lost lease is a silent no-op.
/// </summary>
internal sealed class PostgresBusDispatchStore(
    IAxisBusConnectionFactory connections,
    IAxisLogger<PostgresBusDispatchStore> logger
) : IBusEventDispatchStore
{
    private const string SelectColumns =
        $"{BusEventsTable.EventId}, {BusEventsTable.OrderingKey}, {BusEventsTable.EnqueueSeq}, {BusEventsTable.EventType}, " +
        $"{BusEventsTable.PayloadJson}, {BusEventsTable.Topics}, {BusEventsTable.TraceId}, {BusEventsTable.JourneyId}, {BusEventsTable.CreatedAt}";

    public async Task<IReadOnlyList<OutboxEvent>> ClaimHeadsAsync(string runner, int leaseSeconds, int batchSize, CancellationToken cancellationToken)
    {
        try
        {
            var headIds = await DiscoverHeadsAsync(batchSize, cancellationToken);
            if (headIds.Count == 0)
                return [];

            List<string> claimedIds = [];
            foreach (var id in headIds)
                if (await TryClaimAsync(id, runner, leaseSeconds, cancellationToken))
                    claimedIds.Add(id);

            return claimedIds.Count == 0 ? [] : await ReadBackAsync(claimedIds, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(PostgresBusDispatchStore)}.{nameof(ClaimHeadsAsync)} failed");
            return [];
        }
    }

    private async Task<List<string>> DiscoverHeadsAsync(int batchSize, CancellationToken cancellationToken)
    {
        await using var conn = await connections.OpenConnectionAsync(cancellationToken);
        await using var cmd = Command(conn,
            $"""
             SELECT {BusEventsTable.EventId} FROM (
                 SELECT DISTINCT ON ({BusEventsTable.OrderingKey})
                     {BusEventsTable.EventId}, {BusEventsTable.AvailableAt}, {BusEventsTable.ClaimedUntil}
                 FROM {BusEventsTable.Table}
                 ORDER BY {BusEventsTable.OrderingKey}, {BusEventsTable.CreatedAt}, {BusEventsTable.EnqueueSeq}, {BusEventsTable.EventId}
             ) heads
             WHERE {BusEventsTable.AvailableAt} <= NOW()
               AND ({BusEventsTable.ClaimedUntil} IS NULL OR {BusEventsTable.ClaimedUntil} < NOW())
             LIMIT @batch
             """,
            ("batch", batchSize));

        List<string> ids = [];
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            ids.Add(reader.GetString(0));
        return ids;
    }

    private async Task<bool> TryClaimAsync(string id, string runner, int leaseSeconds, CancellationToken cancellationToken)
    {
        await using var conn = await connections.OpenConnectionAsync(cancellationToken);
        await using var cmd = Command(conn,
            $"""
             UPDATE {BusEventsTable.Table}
             SET {BusEventsTable.ClaimedBy} = @runner,
                 {BusEventsTable.ClaimedUntil} = NOW() + make_interval(secs => @lease)
             WHERE {BusEventsTable.EventId} = @id
               AND ({BusEventsTable.ClaimedUntil} IS NULL OR {BusEventsTable.ClaimedUntil} < NOW())
             """,
            ("runner", runner),
            ("lease", leaseSeconds),
            ("id", id));

        return await cmd.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private async Task<IReadOnlyList<OutboxEvent>> ReadBackAsync(List<string> ids, CancellationToken cancellationToken)
    {
        await using var conn = await connections.OpenConnectionAsync(cancellationToken);
        var names = new string[ids.Count];
        (string Name, object Value)[] parameters = new (string, object)[ids.Count];
        for (var i = 0; i < ids.Count; i++)
        {
            names[i] = $"@id{i}";
            parameters[i] = ($"id{i}", ids[i]);
        }

        await using var cmd = Command(conn,
            $"SELECT {SelectColumns} FROM {BusEventsTable.Table} WHERE {BusEventsTable.EventId} IN ({string.Join(", ", names)})",
            parameters);

        List<OutboxEvent> rows = [];
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(Map(reader));
        return rows;
    }

    public async Task<AxisResult> DeleteDispatchedAsync(string eventId, string runner, CancellationToken cancellationToken)
    {
        try
        {
            await using var conn = await connections.OpenConnectionAsync(cancellationToken);
            await using var cmd = Command(conn,
                $"DELETE FROM {BusEventsTable.Table} WHERE {BusEventsTable.EventId} = @id AND {BusEventsTable.ClaimedBy} = @runner",
                ("id", eventId),
                ("runner", runner));

            await cmd.ExecuteNonQueryAsync(cancellationToken);
            return AxisResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(PostgresBusDispatchStore)}.{nameof(DeleteDispatchedAsync)} failed");
            return AxisError.InternalServerError(AxisBusErrors.PersistenceFailed);
        }
    }

    public async Task<AxisResult> ReleaseAsync(string eventId, string runner, CancellationToken cancellationToken)
    {
        try
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
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(PostgresBusDispatchStore)}.{nameof(ReleaseAsync)} failed");
            return AxisError.InternalServerError(AxisBusErrors.PersistenceFailed);
        }
    }

    // Column order mirrors SelectColumns, which mirrors OutboxEvent's constructor order — read positionally.
    private OutboxEvent Map(DbDataReader r) => new(
        EventId: r.GetString(0),
        OrderingKey: r.GetString(1),
        EnqueueSeq: r.GetInt32(2),
        EventType: r.GetString(3),
        PayloadJson: r.GetString(4),
        Topics: DeserializeTopics(r.GetString(5)),
        TraceId: r.IsDBNull(6) ? null : r.GetString(6),
        JourneyId: r.IsDBNull(7) ? null : r.GetString(7),
        CreatedAt: ReadUtc(r, 8));

    private string[] DeserializeTopics(string json)
    {
        var result = AxisBusSerializer.Deserialize<string[]>(json);
        if (result.IsSuccess)
            return result.Value;

        logger.LogWarning($"{nameof(PostgresBusDispatchStore)} failed to deserialize {BusEventsTable.Topics} json; defaulting to empty.");
        return [];
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
