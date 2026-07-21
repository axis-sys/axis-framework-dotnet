namespace Scaffolds.ECommerce.Contracts.Driving.Customers.v1.RevertEmailValidated;

/// <summary>Confirmation of the reverted mark.</summary>
public sealed record RevertEmailValidatedResponse : IAxisCommandResponse
{
    /// <summary>Id of the customer.</summary>
    public required string CustomerId { get; init; }

    /// <summary>Always false after the revert lands.</summary>
    public required bool EmailValidated { get; init; }
}
