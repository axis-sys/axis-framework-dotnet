namespace Scaffolds.ECommerce.Contracts.Driving.Customers.v1.GetCustomer;

/// <summary>Reads a single customer by its id.</summary>
public sealed record GetCustomerQuery : IAxisQuery<GetCustomerResponse>
{
    /// <summary>Id of the customer to read.</summary>
    /// <example>0192f3a1-7b3c-7d21-9e5f-3c8a51b60002</example>
    public string? CustomerId { get; init; }
}
