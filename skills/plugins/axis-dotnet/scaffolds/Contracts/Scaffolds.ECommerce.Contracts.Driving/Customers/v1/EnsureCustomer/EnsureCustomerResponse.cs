namespace Scaffolds.ECommerce.Contracts.Driving.Customers.v1.EnsureCustomer;

/// <summary>The customer bound to the external identity, existing or just created.</summary>
public sealed record EnsureCustomerResponse : IAxisCommandResponse
{
    /// <summary>Id of the customer.</summary>
    public required string CustomerId { get; init; }

    /// <summary>Email on record, when known.</summary>
    public required string Email { get; init; }

    /// <summary>Display name on record, when known.</summary>
    public required string Name { get; init; }

    /// <summary>Whether the customer has proven ownership of the email on record.</summary>
    public required bool EmailValidated { get; init; }

    /// <summary>Subject identifier of the external identity the customer is bound to.</summary>
    public required string? ExternalId { get; init; }

    /// <summary> The external identity provider used for the customer, or null if no provider is applicable. </summary>
    public required string? Provider { get; set; }

    /// <summary>Whether the customer holds administrative privileges.</summary>
    public required bool IsAdmin { get; init; }
}
