using System.Text.Json;
using Axis;

namespace AxisCache.Repository;

/// <summary>Central (de)serialization for cached values — exceptions never escape; they become an AxisError.</summary>
internal static class AxisCacheSerializer
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
            return AxisError.InternalServerError(AxisCacheErrors.SerializationFailed);
        }
    }

    public static AxisResult<T> Deserialize<T>(string json)
    {
        try
        {
            var value = JsonSerializer.Deserialize<T>(json, Options);
            return value is null
                ? AxisError.InternalServerError(AxisCacheErrors.DeserializationFailed)
                : AxisResult.Ok(value);
        }
        catch (Exception)
        {
            return AxisError.InternalServerError(AxisCacheErrors.DeserializationFailed);
        }
    }
}
