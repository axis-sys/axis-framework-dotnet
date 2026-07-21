namespace Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.Validate;

/// <summary>
/// Proves ownership of the email on record by echoing back the code it received. A mismatched code
/// fails with a validation rule.
/// </summary>
public sealed record ValidateEmailCommand : IAxisCommand<ValidateEmailResponse>
{
    /// <summary>The one-time code received by email.</summary>
    /// <example>0192f3a1</example>
    public string? Code { get; init; }
}
