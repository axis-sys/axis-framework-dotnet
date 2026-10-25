using Microsoft.AspNetCore.Mvc.Filters;
using Scaffolds.ECommerce.Contracts.Driving.Authorization.v1;
using Scaffolds.ECommerce.Contracts.Driving.Authorization.v1.GetGrants;
using Scaffolds.ECommerce.SharedKernel.Auth;
using Scaffolds.ECommerce.SharedKernel.Shared;

namespace Scaffolds.ECommerce.Host.Authorization;

internal sealed class PermissionAuthorizationFilter(IAuthorizationFacade authorizationFacade) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.ActionDescriptor.EndpointMetadata.OfType<RequirePermissionAttribute>().LastOrDefault() is not { } required)
            return;

        var identity = context.HttpContext.User.FindFirst(AuthClaimTypes.CustomerId)?.Value;
        if (!CustomerId.TryParse(identity, out _))
        {
            context.Result = context.HttpContext.ToProblemDetailsResult(AuthorizationErrors.IdentityRequired);
            return;
        }

        var refusal = await authorizationFacade
            .GetGrantsAsync(new GetGrantsQuery { CustomerId = identity })
            .MatchAsync<GetGrantsResponse, AxisError?>(grants => Refusal(grants, required), _ => AuthorizationErrors.GrantsUnavailable);

        if (refusal is not null)
            context.Result = context.HttpContext.ToProblemDetailsResult(refusal);
    }

    private static AxisError? Refusal(GetGrantsResponse grants, RequirePermissionAttribute required)
        => IsGranted(grants, required.Code) ? null : Denial(required);

    private static AxisError Denial(RequirePermissionAttribute required)
        => required.ConcealsSagaRun ? SharedErrors.SagaRunNotFound : AuthorizationErrors.PermissionDenied;

    private static bool IsGranted(GetGrantsResponse grants, string code)
        => !grants.IsDisabled && (grants.IsAdministrator || (grants.IsMember && grants.Permissions.Contains(code)));
}
