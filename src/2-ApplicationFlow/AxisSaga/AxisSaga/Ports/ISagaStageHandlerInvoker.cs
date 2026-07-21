namespace Axis.Ports;

/// <summary>
/// Resolves and invokes the <c>IAxisSagaStageHandler&lt;TPayload&gt;</c> registered for a given
/// <c>(SagaName, StageName)</c> pair on the runtime payload type, in its own DI scope. Dialect-agnostic
/// — kept behind a port only so the engine never sees the reflection.
/// </summary>
public interface ISagaStageHandlerInvoker
{
    Task<AxisResult<object?>> InvokeAsync(Type payloadType, string sagaName, string stageName, object payload);
}
