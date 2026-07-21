namespace Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.Validate;

/// <summary>Acknowledgement of an accepted email-validation run.</summary>
public sealed record ValidateEmailResponse : IAxisCommandResponse
{
    /// <summary>
    /// Saga id of the validation run. Poll
    /// <c>GET /api/v1/auth/email-validation/validate/{sagaId}</c> until a terminal status.
    /// </summary>
    public required string SagaId { get; init; }

    /// <summary>Status of the validation run when it was accepted.</summary>
    /// <example>Pending</example>
    public required string Status { get; init; }
}
