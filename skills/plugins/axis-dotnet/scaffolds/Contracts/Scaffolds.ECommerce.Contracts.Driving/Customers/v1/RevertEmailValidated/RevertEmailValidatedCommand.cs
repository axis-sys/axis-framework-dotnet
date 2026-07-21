namespace Scaffolds.ECommerce.Contracts.Driving.Customers.v1.RevertEmailValidated;

/// <summary>
/// Reverts an email-validated mark. Called by the auth bounded context as the compensation of a
/// validation run whose later stage failed.
/// </summary>
public sealed record RevertEmailValidatedCommand : IAxisCommand<RevertEmailValidatedResponse>
{
    /// <summary>Id of the customer whose mark is reverted.</summary>
    public string? CustomerId { get; init; }
}
