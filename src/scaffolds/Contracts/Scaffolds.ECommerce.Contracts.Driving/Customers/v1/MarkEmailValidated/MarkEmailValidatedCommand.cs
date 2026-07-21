namespace Scaffolds.ECommerce.Contracts.Driving.Customers.v1.MarkEmailValidated;

/// <summary>
/// Records that the customer proved ownership of the email on record. Called by the auth bounded
/// context after a validation code round-trip succeeds.
/// </summary>
public sealed record MarkEmailValidatedCommand : IAxisCommand<MarkEmailValidatedResponse>
{
    /// <summary>Id of the customer whose email was proven.</summary>
    /// <example>0192f3a1-7b3c-7d21-9e5f-3c8a51b60002</example>
    public string? CustomerId { get; init; }
}
