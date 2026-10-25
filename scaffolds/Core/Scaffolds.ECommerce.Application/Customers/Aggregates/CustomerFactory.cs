using Scaffolds.ECommerce.Contracts.Driven.Customers.Customers;
using Scaffolds.ECommerce.Domain.Customers.Customers;

namespace Scaffolds.ECommerce.Application.Customers.Aggregates;

internal interface ICustomerFactory
{
    sealed record NewArgs
    {
        public required string Email { get; init; }
        public required string Name { get; init; }
        public required bool IsAdmin { get; init; }
        public required string ExternalId { get; init; }
        public required string? Provider { get; init; }
    }

    Task<AxisResult<ICustomerEntityProperties>> CreateAsync(NewArgs args);
}

internal sealed class CustomerFactory(ICustomersWritePort customers) : ICustomerFactory
{
    public Task<AxisResult<ICustomerEntityProperties>> CreateAsync(ICustomerFactory.NewArgs args)
        => ((ICustomerEntityProperties)new CustomerProperties(
                CustomerId.New,
                args.Email,
                args.Name,
                args.IsAdmin,
                args.ExternalId,
                args.Provider,
                EmailValidated: false))
            .Rop()
            .ThenAsync(customers.CreateAsync);
}
