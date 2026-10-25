using Scaffolds.ECommerce.Application.Authorization.Services.GrantsCache;
using Scaffolds.ECommerce.Contracts.Driving.Authorization.v1.GetGrants;

namespace Scaffolds.ECommerce.Application.Authorization.UseCases.GetGrants.v1;

internal sealed class GetGrantsHandler(IGrantsCacheService grantsCache) : IAxisQueryHandler<GetGrantsQuery, GetGrantsResponse>
{
    public Task<AxisResult<GetGrantsResponse>> HandleAsync(GetGrantsQuery query)
    {
        var customerId = (CustomerId)query.CustomerId!;

        return grantsCache
            .GetAsync(customerId)
            .MapAsync(grants => new GetGrantsResponse
            {
                CustomerId = grants.CustomerId,
                IsAdministrator = grants.IsAdministrator,
                IsMember = grants.IsMember,
                IsDisabled = grants.IsDisabled,
                Permissions = grants.Permissions,
            });
    }
}
