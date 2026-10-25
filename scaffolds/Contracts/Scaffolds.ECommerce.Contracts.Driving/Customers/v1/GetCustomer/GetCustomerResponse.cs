namespace Scaffolds.ECommerce.Contracts.Driving.Customers.v1.GetCustomer;

public sealed record GetCustomerResponse : IAxisQueryResponse
{
    public required string CustomerId { get; init; }

    public required string Email { get; init; }

    public required string Name { get; init; }

    public required bool EmailValidated { get; init; }

    public required bool IsAdmin { get; init; }

    public required string? ExternalId { get; init; }
}
