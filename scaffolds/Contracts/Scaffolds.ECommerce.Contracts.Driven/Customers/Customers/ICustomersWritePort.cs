using Scaffolds.ECommerce.Domain.Customers.Customers;
using Scaffolds.ECommerce.SharedKernel.Shared;

namespace Scaffolds.ECommerce.Contracts.Driven.Customers.Customers;

public interface ICustomersWritePort
{
    Task<AxisResult> CreateAsync(ICustomerEntityProperties customer);
    Task<AxisResult> SetEmailValidatedAsync(CustomerId customerId, bool emailValidated);
}
