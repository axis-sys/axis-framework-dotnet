namespace Scaffolds.ECommerce.Contracts.Driving.Customers.v1.GetCustomer;

public sealed record GetCustomerQuery : IAxisQuery<GetCustomerResponse>
{
    public string? CustomerId { get; init; }
}
