using System.Text.Json;
using Axis.SharedKernel;

namespace Axis.Saga.Json;

/// <summary>
/// Wraps <see cref="JsonSerializer"/> with the saga-specific options and converts exceptions into
/// <see cref="AxisResult"/>. Keeps every saga payload (de)serialisation in one place so error
/// handling and option choices stay consistent across the engine and the mediator.
/// </summary>
internal static class SagaPayloadSerializer
{
    public static AxisResult<object> Deserialize(string payloadJson, Type payloadType)
    {
        try
        {
            var payload = JsonSerializer.Deserialize(payloadJson, payloadType, AxisSagaJsonOptions.Default);
            return payload is null
                ? AxisError.InternalServerError(AxisSagaErrors.PayloadDeserializationFailed)
                : AxisResult.Ok(payload);
        }
        catch (Exception)
        {
            return AxisError.InternalServerError(AxisSagaErrors.PayloadDeserializationFailed);
        }
    }

    public static AxisResult<string> Serialize(object payload, Type payloadType)
    {
        try
        {
            var json = JsonSerializer.Serialize(payload, payloadType, AxisSagaJsonOptions.Default);
            return AxisResult.Ok(json);
        }
        catch (Exception)
        {
            return AxisError.InternalServerError(AxisSagaErrors.PayloadSerializationFailed);
        }
    }
}
