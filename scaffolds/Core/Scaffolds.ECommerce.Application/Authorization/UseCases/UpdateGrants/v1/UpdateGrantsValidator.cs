using Scaffolds.ECommerce.Contracts.Driven.Authorization.Members;
using Scaffolds.ECommerce.Contracts.Driving.Authorization.v1.UpdateGrants;
using Scaffolds.ECommerce.SharedKernel.Authorization;

namespace Scaffolds.ECommerce.Application.Authorization.UseCases.UpdateGrants.v1;

internal sealed class UpdateGrantsValidator : AxisValidatorBase<UpdateGrantsCommand>
{
    public UpdateGrantsValidator()
    {
        RequiredTryParse(x => x.CustomerId, SharedErrors.CustomerIdInvalid, CustomerId.TryParse);
        Satisfies(x => x.Disabled, MemberErrors.DisabledRequired, disabled => disabled.HasValue);
        Satisfies(x => x.Permissions, MemberErrors.PermissionsRequired, permissions => permissions is not null);
        EachSatisfies(x => x.Permissions, MemberErrors.PermissionUnknown, permission => permission is not null && PermissionCodes.All.Contains(permission));
    }
}
