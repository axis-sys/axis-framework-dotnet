using Scaffolds.ECommerce.Contracts.Driving.Customers.v1;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1.EnsureCustomer;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1.GetCustomer;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1.MarkEmailValidated;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1.RevertEmailValidated;

namespace Scaffolds.ECommerce.Adapters.Driving.Facade.Customers;

internal sealed class CustomersFacade(IAxisMediator mediator) : ICustomersFacade
{
    public Task<AxisResult<EnsureCustomerResponse>> EnsureCustomerAsync(EnsureCustomerCommand command)
        => mediator.Cqrs.ExecuteAsync<EnsureCustomerCommand, EnsureCustomerResponse>(command);

    public Task<AxisResult<GetCustomerResponse>> GetCustomerAsync(GetCustomerQuery query)
        => mediator.Cqrs.QueryAsync<GetCustomerQuery, GetCustomerResponse>(query);

    public Task<AxisResult<MarkEmailValidatedResponse>> MarkEmailValidatedAsync(MarkEmailValidatedCommand command)
        => mediator.Cqrs.ExecuteAsync<MarkEmailValidatedCommand, MarkEmailValidatedResponse>(command);

    public Task<AxisResult<RevertEmailValidatedResponse>> RevertEmailValidatedAsync(RevertEmailValidatedCommand command)
        => mediator.Cqrs.ExecuteAsync<RevertEmailValidatedCommand, RevertEmailValidatedResponse>(command);
}
