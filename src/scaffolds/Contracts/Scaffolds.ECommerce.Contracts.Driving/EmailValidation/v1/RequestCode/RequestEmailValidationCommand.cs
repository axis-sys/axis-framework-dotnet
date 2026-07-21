namespace Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.RequestCode;

/// <summary>
/// Emails a one-time validation code to the authenticated customer's email on record. The customer
/// comes from the access token, so the command carries no fields.
/// </summary>
public sealed record RequestEmailValidationCommand : IAxisCommand<RequestEmailValidationResponse>;
