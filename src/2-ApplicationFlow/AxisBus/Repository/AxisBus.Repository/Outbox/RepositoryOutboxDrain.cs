using System.Data.Common;
using Axis;
using AxisBus.Repository.Ports;

namespace AxisBus.Repository.Outbox;

/// <summary>
/// The bridge from the bus to the repository: the <see cref="IAxisRepositoryOutbox"/> the unit of work invokes
/// just before COMMIT. It drains the request-scoped queue and INSERTs each event into the unit of work's OWN
/// connection/transaction (passed in) via the dialect's <see cref="IAxisBusSqlDialect.InsertSql"/>, so the
/// events are committed atomically with the business state — no separate connection, no autocommit. Never
/// throws; a failure returns an <see cref="AxisResult"/> error, which aborts the commit (nothing persists).
/// </summary>
internal sealed class RepositoryOutboxDrain(
    IOutboxScopedQueue queue,
    IAxisBusSqlDialect dialect,
    IAxisLogger<RepositoryOutboxDrain> logger
) : IAxisRepositoryOutbox
{
    public async Task<AxisResult> DrainAsync(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken)
    {
        var pending = queue.DrainAll();
        if (pending.Count == 0)
            return AxisResult.Ok();

        try
        {
            foreach (var e in pending)
            {
                var topicsJson = AxisBusSerializer.Serialize(e.Topics);
                if (topicsJson.IsFailure)
                    return AxisResult.Error(topicsJson.Errors);

                var createdAtUtc = e.CreatedAt.UtcDateTime;
                await using var cmd = Command(connection, transaction, dialect.InsertSql,
                    ("eventId", e.EventId),
                    ("orderingKey", e.OrderingKey),
                    ("enqueueSeq", e.EnqueueSeq),
                    ("eventType", e.EventType),
                    ("payloadJson", e.PayloadJson),
                    ("topics", topicsJson.Value),
                    ("traceId", (object?)e.TraceId ?? DBNull.Value),
                    ("journeyId", (object?)e.JourneyId ?? DBNull.Value),
                    ("createdAt", createdAtUtc),
                    ("availableAt", createdAtUtc));

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            return AxisResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(RepositoryOutboxDrain)}.{nameof(DrainAsync)} failed");
            return AxisError.InternalServerError(AxisBusErrors.PersistenceFailed);
        }
    }

    private static DbCommand Command(DbConnection conn, DbTransaction tx, string sql, params (string Name, object Value)[] parameters)
    {
        var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
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
