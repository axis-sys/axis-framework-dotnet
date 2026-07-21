using System.Text.Json;
using Axis;

namespace AxisBus.Repository;

/// <summary>
/// Central (de)serialization for outbox payloads — exceptions never escape; they become an AxisError. Mirrors
/// <c>AxisCacheSerializer</c>. The publish path only serializes; the deserialize methods are consumed by the
/// dispatch path — <see cref="BusDispatcher"/> rehydrates the event before invoking handlers (via the
/// non-generic overload) and the dispatch store deserializes the topics list with <see cref="Deserialize{T}"/>.
/// </summary>
internal static class AxisBusSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static AxisResult<string> Serialize<T>(T value)
    {
        try
        {
            return AxisResult.Ok(JsonSerializer.Serialize(value, Options));
        }
        catch (Exception)
        {
            return AxisError.InternalServerError(AxisBusErrors.SerializationFailed);
        }
    }

    public static AxisResult<T> Deserialize<T>(string json)
    {
        try
        {
            var value = JsonSerializer.Deserialize<T>(json, Options);
            return value is null
                ? AxisError.InternalServerError(AxisBusErrors.DeserializationFailed)
                : AxisResult.Ok(value);
        }
        catch (Exception)
        {
            return AxisError.InternalServerError(AxisBusErrors.DeserializationFailed);
        }
    }

    /// <summary>
    /// Non-generic twin of <see cref="Deserialize{T}"/> for the dispatcher, which only learns the event's
    /// CLR <see cref="Type"/> at runtime (via <c>Type.GetType(OutboxEvent.EventType)</c>) — a generic type
    /// parameter is not available there.
    /// </summary>
    public static AxisResult<object> Deserialize(string json, Type type)
    {
        try
        {
            var value = JsonSerializer.Deserialize(json, type, Options);
            return value is null
                ? AxisError.InternalServerError(AxisBusErrors.DeserializationFailed)
                : AxisResult.Ok(value);
        }
        catch (Exception)
        {
            return AxisError.InternalServerError(AxisBusErrors.DeserializationFailed);
        }
    }
}
