using Scaffolds.ECommerce.Domain.Customers.Customers;
using Scaffolds.ECommerce.SharedKernel.Shared;

namespace Scaffolds.ECommerce.Contracts.Driven.Customers.Customers;

public interface ICustomersReaderPort
{
    Task<AxisResult<ICustomerEntityProperties>> GetByIdAsync(CustomerId customerId);
    Task<AxisResult<ICustomerEntityProperties>> GetByEmailAsync(string email);
}
