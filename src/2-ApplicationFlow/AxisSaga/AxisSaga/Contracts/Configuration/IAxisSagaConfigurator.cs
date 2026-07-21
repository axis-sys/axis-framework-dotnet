namespace Axis.Contracts.Configuration;

public interface IAxisSagaConfigurator<out TPayload> where TPayload : class
{
    IAxisSagaStageBuilder<TPayload> AddStage(string stageName);
    IAxisSagaStageBuilder<TPayload> AddErrorStage(string stageName);
}
