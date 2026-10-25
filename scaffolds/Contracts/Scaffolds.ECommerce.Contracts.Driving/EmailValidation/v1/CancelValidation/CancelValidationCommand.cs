namespace Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.CancelValidation;

public sealed record CancelValidationCommand : IAxisCommand<CancelValidationResponse>
{
    [JsonIgnore]
    public string? SagaId { get; init; }
}
