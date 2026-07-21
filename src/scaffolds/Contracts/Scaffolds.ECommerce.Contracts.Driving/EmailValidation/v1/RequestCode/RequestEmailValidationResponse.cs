namespace Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.RequestCode;

/// <summary>Confirmation that the validation code was sent.</summary>
public sealed record RequestEmailValidationResponse : IAxisCommandResponse
{
    /// <summary>Email address the code was sent to.</summary>
    public required string SentTo { get; init; }
}
