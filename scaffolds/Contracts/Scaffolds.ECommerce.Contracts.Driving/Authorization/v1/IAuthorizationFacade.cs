using Scaffolds.ECommerce.Contracts.Driving.Authorization.v1.GetGrants;
using Scaffolds.ECommerce.Contracts.Driving.Authorization.v1.UpdateGrants;

namespace Scaffolds.ECommerce.Contracts.Driving.Authorization.v1;

public interface IAuthorizationFacade
{
    Task<AxisResult<GetGrantsResponse>> GetGrantsAsync(GetGrantsQuery query);
    Task<AxisResult<UpdateGrantsResponse>> UpdateGrantsAsync(UpdateGrantsCommand command);
}
