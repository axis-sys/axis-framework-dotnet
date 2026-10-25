namespace Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.GetValidationStatus;

public sealed record GetValidationStatusQuery : IAxisQuery<GetValidationStatusResponse>
{
    [JsonIgnore]
    public string? SagaId { get; init; }
}
