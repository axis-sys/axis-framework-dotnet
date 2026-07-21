using AxisBus.Repository.Persistence;
using AxisBus.Repository.Ports;

namespace AxisBus.Postgres;

/// <summary>The single divergent statement for Postgres: an INSERT casting the JSON payload/topics to JSONB.</summary>
internal sealed class PostgresBusSqlDialect : IAxisBusSqlDialect
{
    public string InsertSql { get; } =
        $"""
         INSERT INTO {BusEventsTable.Table}
             ({BusEventsTable.EventId}, {BusEventsTable.OrderingKey}, {BusEventsTable.EnqueueSeq}, {BusEventsTable.EventType},
              {BusEventsTable.PayloadJson}, {BusEventsTable.Topics}, {BusEventsTable.TraceId}, {BusEventsTable.JourneyId},
              {BusEventsTable.CreatedAt}, {BusEventsTable.AvailableAt})
         VALUES (@eventId, @orderingKey, @enqueueSeq, @eventType, @payloadJson::JSONB, @topics::JSONB, @traceId, @journeyId, @createdAt, @availableAt)
         """;
}
