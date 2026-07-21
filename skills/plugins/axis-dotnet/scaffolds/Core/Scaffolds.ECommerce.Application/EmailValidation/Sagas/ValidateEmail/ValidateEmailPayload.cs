namespace Scaffolds.ECommerce.Application.EmailValidation.Sagas.ValidateEmail;

internal sealed record ValidateEmailPayload
{
    public required string CustomerId { get; init; }

    public bool EmailMarkedValidated { get; init; }
    public bool EmailWasAlreadyValidated { get; init; }
    public bool CodeRemoved { get; init; }
    public bool EmailValidationReverted { get; init; }
}
