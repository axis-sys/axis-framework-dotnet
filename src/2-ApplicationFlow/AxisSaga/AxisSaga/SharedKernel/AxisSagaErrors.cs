namespace Axis.SharedKernel;

public static class AxisSagaErrors
{
    public const string SagaDefinitionNotFound = "SAGA_DEFINITION_NOT_FOUND";
    public const string SagaInstanceNotFound = "SAGA_INSTANCE_NOT_FOUND";
    public const string StageHandlerNotFound = "SAGA_STAGE_HANDLER_NOT_FOUND";
    public const string StageNotFound = "SAGA_STAGE_NOT_FOUND";
    public const string ConcurrencyConflict = "SAGA_CONCURRENCY_CONFLICT";
    public const string PayloadDeserializationFailed = "SAGA_PAYLOAD_DESERIALIZATION_FAILED";
    public const string PayloadSerializationFailed = "SAGA_PAYLOAD_SERIALIZATION_FAILED";
    public const string PersistenceFailed = "SAGA_PERSISTENCE_FAILED";
    public const string SagaSettingsNotFound = "SAGA_SETTINGS_NOT_FOUND";
    public const string InvalidConcurrencyCap = "SAGA_INVALID_CONCURRENCY_CAP";
}
