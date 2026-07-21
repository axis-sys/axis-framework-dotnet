using CustomerId = Scaffolds.ECommerce.SharedKernel.ContextIds.CustomerId;

namespace Scaffolds.ECommerce.Contracts.Driven.Customers;

public interface ICustomersPort
{
    Task<AxisResult<ICustomerEntityProperties>> GetByIdAsync(CustomerId customerId);
    Task<AxisResult<ICustomerEntityProperties>> GetByEmailAsync(string email);
    Task<AxisResult> CreateAsync(ICustomerEntityProperties properties);
    Task<AxisResult> SetEmailValidatedAsync(CustomerId customerId, bool emailValidated);
}
