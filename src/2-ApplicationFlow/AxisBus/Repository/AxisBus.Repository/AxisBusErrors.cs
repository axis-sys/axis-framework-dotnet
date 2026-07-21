namespace AxisBus.Repository;

internal static class AxisBusErrors
{
    public const string PersistenceFailed = "AXIS_BUS_PERSISTENCE_FAILED";
    public const string SerializationFailed = "AXIS_BUS_SERIALIZATION_FAILED";
    public const string DeserializationFailed = "AXIS_BUS_DESERIALIZATION_FAILED";
    public const string EventTypeResolutionFailed = "AXIS_BUS_EVENT_TYPE_RESOLUTION_FAILED";
    public const string HandlerInvocationFailed = "AXIS_BUS_HANDLER_INVOCATION_FAILED";
}
