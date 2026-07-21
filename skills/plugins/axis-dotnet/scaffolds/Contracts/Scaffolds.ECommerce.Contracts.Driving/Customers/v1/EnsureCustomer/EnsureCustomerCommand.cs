namespace Scaffolds.ECommerce.Contracts.Driving.Customers.v1.EnsureCustomer;

/// <summary>
/// Finds the customer bound to an external identity, creating it on first sight (idempotent).
/// Called by the auth bounded context whenever an external provider authenticates a user.
/// </summary>
public sealed record EnsureCustomerCommand : IAxisCommand<EnsureCustomerResponse>
{
    /// <summary>Stable subject identifier issued by the external identity provider.</summary>
    /// <example>0192f3a1-7b3c-7d21-9e5f-3c8a51b60001</example>
    public string? ExternalId { get; init; }

    /// <summary>Email carried by the external identity, when the provider shares it.</summary>
    /// <example>ada.lovelace@example.com</example>
    public string? Email { get; init; }

    /// <summary>Display name carried by the external identity, when the provider shares it.</summary>
    /// <example>Ada Lovelace</example>
    public string? Name { get; init; }

    /// <summary>
    /// The external identity provider that authenticated the user.
    /// </summary>
    public string? Provider { get; init; }
}
