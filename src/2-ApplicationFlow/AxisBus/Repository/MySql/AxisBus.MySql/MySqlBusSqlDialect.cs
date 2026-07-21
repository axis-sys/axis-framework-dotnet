using AxisBus.Repository.Persistence;
using AxisBus.Repository.Ports;

namespace AxisBus.MySql;

/// <summary>The single divergent statement for MySQL: a plain INSERT, no <c>::JSONB</c> cast needed (mirrors <c>MySqlCacheSqlDialect</c>).</summary>
internal sealed class MySqlBusSqlDialect : IAxisBusSqlDialect
{
    public string InsertSql { get; } =
        $"""
         INSERT INTO {BusEventsTable.Table}
             ({BusEventsTable.EventId}, {BusEventsTable.OrderingKey}, {BusEventsTable.EnqueueSeq}, {BusEventsTable.EventType},
              {BusEventsTable.PayloadJson}, {BusEventsTable.Topics}, {BusEventsTable.TraceId}, {BusEventsTable.JourneyId},
              {BusEventsTable.CreatedAt}, {BusEventsTable.AvailableAt})
         VALUES (@eventId, @orderingKey, @enqueueSeq, @eventType, @payloadJson, @topics, @traceId, @journeyId, @createdAt, @availableAt)
         """;
}
