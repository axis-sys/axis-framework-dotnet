using Axis.SharedKernel;

namespace Axis.Ports;

public interface IAxisSagaMediator
{
    Task<AxisResult<string>> StartAsync<TPayload>(string sagaName, TPayload payload)
        where TPayload : class;

    /// <summary>
    /// Starts a saga with a caller-supplied id instead of a generated one, so the caller can use the
    /// same id as a correlation key in its own domain (e.g. a batch/run id). The default delegates to
    /// the generated-id overload, so existing implementers keep compiling.
    /// </summary>
    Task<AxisResult<string>> StartAsync<TPayload>(string sagaName, TPayload payload, string sagaId)
        where TPayload : class
        => StartAsync(sagaName, payload);

    Task<AxisResult<string>> StartAsync<TPayload>(string sagaName, TPayload payload, TimeSpan? retainedFor)
        where TPayload : class
        => StartAsync(sagaName, payload);

    Task<AxisResult<string>> StartAsync<TPayload>(string sagaName, TPayload payload, string sagaId, TimeSpan? retainedFor)
        where TPayload : class
        => StartAsync(sagaName, payload, sagaId);

    Task<AxisResult<AxisSagaInstance>> GetByIdAsync(string sagaId);
    Task<AxisResult<AxisSagaInstance<TPayload>>> GetByIdAsync<TPayload>(string sagaId) where TPayload : class;

    Task<AxisResult> ResumeAsync(string sagaId);
}
