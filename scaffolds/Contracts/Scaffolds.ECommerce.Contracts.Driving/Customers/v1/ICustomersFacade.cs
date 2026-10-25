using Scaffolds.ECommerce.Contracts.Driving.Customers.v1.EnsureCustomer;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1.GetCustomer;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1.MarkEmailValidated;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1.RevertEmailValidated;

namespace Scaffolds.ECommerce.Contracts.Driving.Customers.v1;

public interface ICustomersFacade
{
    Task<AxisResult<EnsureCustomerResponse>> EnsureCustomerAsync(EnsureCustomerCommand command);
    Task<AxisResult<GetCustomerResponse>> GetCustomerAsync(GetCustomerQuery query);
    Task<AxisResult<MarkEmailValidatedResponse>> MarkEmailValidatedAsync(MarkEmailValidatedCommand command);
    Task<AxisResult<RevertEmailValidatedResponse>> RevertEmailValidatedAsync(RevertEmailValidatedCommand command);
}
