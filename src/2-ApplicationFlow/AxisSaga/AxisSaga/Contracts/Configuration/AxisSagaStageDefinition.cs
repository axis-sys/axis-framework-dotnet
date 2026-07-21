namespace Axis.Contracts.Configuration;

public record AxisSagaStageDefinition
{
    public required string StageName { get; init; }

    public required bool IsErrorStage { get; init; }

    public string? NextStageOnSuccess { get; init; }

    public required IReadOnlyList<string> RouteToOnError { get; init; }

    public int? TransientRetryMaxAttempts { get; init; }

    public TimeSpan? TransientRetryBaseDelay { get; init; }
}
