namespace Scaffolds.ECommerce.Contracts.Driving.Customers.v1.EnsureCustomer;

public sealed record EnsureCustomerCommand : IAxisCommand<EnsureCustomerResponse>
{
    public string? ExternalId { get; init; }

    public string? Email { get; init; }

    public string? Name { get; init; }

    public string? Provider { get; init; }
}
