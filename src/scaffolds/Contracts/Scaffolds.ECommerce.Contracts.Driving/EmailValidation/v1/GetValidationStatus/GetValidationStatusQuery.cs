namespace Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.GetValidationStatus;

/// <summary>Reads the status of a validation run accepted by <c>POST /api/v1/auth/email-validation/validate</c>.</summary>
public sealed record GetValidationStatusQuery : IAxisQuery<GetValidationStatusResponse>
{
    /// <summary>Saga id returned when the validation was accepted. Injected from the route, never from the body.</summary>
    [JsonIgnore]
    public string? SagaId { get; init; }
}
