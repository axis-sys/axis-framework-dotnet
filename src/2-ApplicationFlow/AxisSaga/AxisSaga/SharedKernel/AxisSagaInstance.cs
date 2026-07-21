namespace Axis.SharedKernel;

public record AxisSagaInstance
{
    public required string SagaId { get; init; }
    public required string SagaName { get; init; }
    public required AxisSagaStatus Status { get; init; }
    public string? CurrentStage { get; init; }
    public required string PayloadJson { get; init; }
    public string? LastErrorCode { get; init; }
    public string? LastErrorMessage { get; init; }
    public required int Version { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}

public record AxisSagaInstance<TPayload> : AxisSagaInstance where TPayload : class
{
    public required TPayload Payload { get; init; }
}
