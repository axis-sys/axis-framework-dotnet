using Scaffolds.ECommerce.Application.Authorization.Aggregates;
using Scaffolds.ECommerce.Contracts.Driving.Authorization.v1.UpdateGrants;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1.GetCustomer;
using Scaffolds.ECommerce.SharedKernel.Authorization.Events.v1;

namespace Scaffolds.ECommerce.Application.Authorization.UseCases.UpdateGrants.v1;

internal sealed class UpdateGrantsHandler(
    ICustomersFacade customersFacade,
    IMemberFactory memberFactory,
    IAxisBus bus,
    IUnitOfWork unitOfWork
) : IAxisCommandHandler<UpdateGrantsCommand, UpdateGrantsResponse>
{
    public Task<AxisResult<UpdateGrantsResponse>> HandleAsync(UpdateGrantsCommand command)
    {
        var customerId = (CustomerId)command.CustomerId!;

        return customersFacade
            .GetCustomerAsync(new GetCustomerQuery { CustomerId = customerId })
            .ThenAsync(_ => memberFactory.CreateAsync(new IMemberFactory.NewArgs
            {
                CustomerId = customerId,
                Disabled = command.Disabled!.Value,
                Permissions = command.Permissions!,
            }))
            .ThenAsync(member => bus.PublishAsync(new GrantsUpdatedEvent(member.CustomerId), GrantsUpdatedEvent.Topic))
            .ThenAsync(_ => unitOfWork.SaveChangesAsync())
            .MapAsync(member => new UpdateGrantsResponse
            {
                CustomerId = member.CustomerId,
                Disabled = member.Disabled,
                Permissions = member.Permissions,
            });
    }
}
