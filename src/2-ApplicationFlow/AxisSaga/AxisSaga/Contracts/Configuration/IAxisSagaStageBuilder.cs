namespace Axis.Contracts.Configuration;

public interface IAxisSagaStageBuilder<out TPayload> where TPayload : class
{
    IAxisSagaStageBuilder<TPayload> NextStageOnSuccess(string nextStageName);
    IAxisSagaStageBuilder<TPayload> FinishOnSuccess();
    IAxisSagaStageBuilder<TPayload> RouteToOnError(params string[] errorStageNames);
    IAxisSagaStageBuilder<TPayload> RetryOnTransient(int maxAttempts, TimeSpan? baseDelay = null);
}
