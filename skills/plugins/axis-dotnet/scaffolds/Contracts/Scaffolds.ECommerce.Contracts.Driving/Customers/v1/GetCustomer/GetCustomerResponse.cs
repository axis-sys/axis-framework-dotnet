namespace Scaffolds.ECommerce.Contracts.Driving.Customers.v1.GetCustomer;

/// <summary>A customer as returned by the read use cases.</summary>
public sealed record GetCustomerResponse : IAxisQueryResponse
{
    /// <summary>Id of the customer.</summary>
    public required string CustomerId { get; init; }

    /// <summary>Email on record, when known.</summary>
    public string? Email { get; init; }

    /// <summary>Display name on record, when known.</summary>
    public string? Name { get; init; }

    /// <summary>Whether the customer has proven ownership of the email on record.</summary>
    public required bool EmailValidated { get; init; }

    /// <summary>Whether the customer holds administrative privileges.</summary>
    public required bool IsAdmin { get; init; }
}
