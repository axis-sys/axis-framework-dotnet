using Axis.Contracts.Configuration;

namespace Axis.Core;

internal class AxisSagaConfigurator<TPayload>(string sagaName) : IAxisSagaConfigurator<TPayload> where TPayload : class
{
    private readonly List<AxisSagaStageBuilder<TPayload>> _forwardStages = [];
    private readonly List<AxisSagaStageBuilder<TPayload>> _errorStages = [];

    public IAxisSagaStageBuilder<TPayload> AddStage(string stageName)
    {
        EnsureStageNameValid(stageName);
        EnsureStageNameUnique(stageName);

        var builder = new AxisSagaStageBuilder<TPayload>(stageName, isErrorStage: false);
        _forwardStages.Add(builder);
        return builder;
    }

    public IAxisSagaStageBuilder<TPayload> AddErrorStage(string stageName)
    {
        EnsureStageNameValid(stageName);
        EnsureStageNameUnique(stageName);

        var builder = new AxisSagaStageBuilder<TPayload>(stageName, isErrorStage: true);
        _errorStages.Add(builder);
        return builder;
    }

    internal AxisSagaDefinition Build()
    {
        if (_forwardStages.Count == 0)
            throw new InvalidOperationException($"Saga '{sagaName}' must have at least one forward stage (use AddStage).");

        var forward = _forwardStages.Select(b => b.Compile()).ToList().AsReadOnly();
        var errors = _errorStages.Select(b => b.Compile()).ToList().AsReadOnly();

        ValidateRoutes(forward, errors);

        return new AxisSagaDefinition
        {
            SagaName = sagaName,
            PayloadType = typeof(TPayload),
            ForwardStages = forward,
            ErrorStages = errors
        };
    }

    private static void EnsureStageNameValid(string stageName)
    {
        if (string.IsNullOrWhiteSpace(stageName))
            throw new ArgumentException("Stage name cannot be null or empty.", nameof(stageName));
    }

    private void EnsureStageNameUnique(string stageName)
    {
        var existing = _forwardStages.Any(b => b.StageName == stageName)
                       || _errorStages.Any(b => b.StageName == stageName);
        if (existing)
            throw new InvalidOperationException($"Saga '{sagaName}' already has a stage named '{stageName}'.");
    }

    private void ValidateRoutes(
        IReadOnlyList<AxisSagaStageDefinition> forward,
        IReadOnlyList<AxisSagaStageDefinition> errors
    )
    {
        var allStageNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in forward) allStageNames.Add(s.StageName);
        foreach (var s in errors) allStageNames.Add(s.StageName);

        foreach (var stage in forward.Concat(errors))
        {
            if (stage.NextStageOnSuccess is { } next && !allStageNames.Contains(next))
                throw new InvalidOperationException($"Saga '{sagaName}' stage '{stage.StageName}' NextStageOnSuccess references unknown stage '{next}'.");

            foreach (var routed in stage.RouteToOnError)
            {
                if (!allStageNames.Contains(routed))
                    throw new InvalidOperationException($"Saga '{sagaName}' stage '{stage.StageName}' RouteToOnError references unknown stage '{routed}'.");
            }
        }
    }
}
