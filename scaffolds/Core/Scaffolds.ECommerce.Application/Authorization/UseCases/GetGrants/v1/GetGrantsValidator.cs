using Scaffolds.ECommerce.Contracts.Driving.Authorization.v1.GetGrants;

namespace Scaffolds.ECommerce.Application.Authorization.UseCases.GetGrants.v1;

internal sealed class GetGrantsValidator : AxisValidatorBase<GetGrantsQuery>
{
    public GetGrantsValidator()
    {
        RequiredTryParse(x => x.CustomerId, SharedErrors.CustomerIdInvalid, CustomerId.TryParse);
    }
}
