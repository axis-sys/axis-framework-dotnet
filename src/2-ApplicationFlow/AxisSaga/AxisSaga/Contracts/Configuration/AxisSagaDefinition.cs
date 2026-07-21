namespace Axis.Contracts.Configuration;

public record AxisSagaDefinition
{
    public required string SagaName { get; init; }

    public required Type PayloadType { get; init; }

    public AxisSagaStageDefinition FirstForwardStage => ForwardStages[0];
    public required IReadOnlyList<AxisSagaStageDefinition> ForwardStages { get; init; }

    public required IReadOnlyList<AxisSagaStageDefinition> ErrorStages { get; init; }

    public AxisSagaStageDefinition? GetStage(string stageName)
    {
        foreach (var stage in ForwardStages)
            if (stage.StageName == stageName) return stage;

        return ErrorStages.FirstOrDefault(stage => stage.StageName == stageName);
    }
}
