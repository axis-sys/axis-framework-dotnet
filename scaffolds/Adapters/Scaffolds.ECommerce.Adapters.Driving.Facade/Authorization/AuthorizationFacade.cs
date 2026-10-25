using Scaffolds.ECommerce.Contracts.Driving.Authorization.v1;
using Scaffolds.ECommerce.Contracts.Driving.Authorization.v1.GetGrants;
using Scaffolds.ECommerce.Contracts.Driving.Authorization.v1.UpdateGrants;

namespace Scaffolds.ECommerce.Adapters.Driving.Facade.Authorization;

internal sealed class AuthorizationFacade(IAxisMediator mediator) : IAuthorizationFacade
{
    public Task<AxisResult<GetGrantsResponse>> GetGrantsAsync(GetGrantsQuery query)
        => mediator.Cqrs.QueryAsync<GetGrantsQuery, GetGrantsResponse>(query);

    public Task<AxisResult<UpdateGrantsResponse>> UpdateGrantsAsync(UpdateGrantsCommand command)
        => mediator.Cqrs.ExecuteAsync<UpdateGrantsCommand, UpdateGrantsResponse>(command);
}
