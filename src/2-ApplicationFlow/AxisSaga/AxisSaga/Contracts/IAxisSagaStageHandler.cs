namespace Axis.Contracts;

public interface IAxisSagaStageHandler<TPayload> where TPayload : class
{
    string SagaName { get; }
    string StageName { get; }

    /// <summary>
    /// Executes the stage. MUST be idempotent: the engine is at-least-once, and on crash-resume — or if
    /// the execution lease lapses under load and the resumer re-fires the saga — the same stage can run
    /// again (or, briefly, concurrently). Short-circuit when the work is already done (e.g. the payload
    /// already carries the id this stage would create) so a re-run is a harmless no-op.
    /// </summary>
    Task<AxisResult<TPayload>> ExecuteAsync(TPayload payload);
}
